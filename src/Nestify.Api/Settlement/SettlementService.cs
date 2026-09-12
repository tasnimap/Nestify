using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Api.Homes;
using Nestify.Shared.Dtos.Settlement;

namespace Nestify.Api.Settlement;

public sealed class SettlementService
{
    private const short EqualSplit = 1;
    private const short MealPurchase = 2;
    private const short MealFund = 1;
    private const short SharedBills = 2;
    private const short Finalized = 2;

    private readonly DbConnectionFactory _db;

    public SettlementService(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<SettlementWorkspaceDto?> GetAsync(long userId, int year, int month)
    {
        ValidatePeriod(year, month);
        using var connection = await _db.OpenAsync();
        var membership = await GetMembershipAsync(connection, null, userId);
        if (membership is null)
        {
            return null;
        }

        return await LoadWorkspaceAsync(connection, null, membership.HomeId, userId, year, month);
    }

    public async Task<(SettlementBillDto? Data, string? Error)> AddBillAsync(
        long userId, int year, int month, CreateSettlementBillRequest request)
    {
        ValidatePeriod(year, month);
        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length is < 1 or > 200 || request.Amount <= 0m)
        {
            return (null, "A bill description and a positive amount are required.");
        }

        using var connection = await _db.OpenAsync();
        var membership = await GetManagerMembershipAsync(connection, userId);
        if (membership is null)
        {
            return (null, "Only a home manager or co-manager can add bills.");
        }

        if (await IsFinalizedAsync(connection, null, membership.HomeId, year, month))
        {
            return (null, "This settlement period is finalized.");
        }

        if (!TryNormalizeDate(request.SpentOn, year, month, out var spentOn))
        {
            return (null, "The bill date must belong to the selected settlement period.");
        }
        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO expenses (house_id, category, description, amount, spent_by_user_id,
                                  spent_on, period_year, period_month, created_by_user_id)
            VALUES (@homeId, @category, @description, @amount, @userId,
                    @spentOn, @year, @month, @userId)
            RETURNING id
            """,
            new { homeId = membership.HomeId, category = EqualSplit, description,
                amount = request.Amount, userId, spentOn, year, month });

        return (await connection.QuerySingleAsync<SettlementBillDto>(
            """
            SELECT e.id, e.description, e.amount, e.spent_on AS SpentOn,
                   e.spent_by_user_id AS SpentByUserId, u.full_name AS SpentByName
            FROM expenses e JOIN users u ON u.id = e.spent_by_user_id
            WHERE e.id = @id
            """, new { id }), null);
    }

    public async Task<(SettlementPaymentDto? Data, string? Error)> AddPaymentAsync(
        long userId, int year, int month, CreateSettlementPaymentRequest request)
    {
        ValidatePeriod(year, month);
        var note = (request.Note ?? string.Empty).Trim();
        if (request.UserId <= 0 || request.Amount <= 0m || request.FundType is < MealFund or > SharedBills || note.Length is < 1 or > 200)
        {
            return (null, "A member, positive amount, payment type, and note are required.");
        }

        using var connection = await _db.OpenAsync();
        var membership = await GetManagerMembershipAsync(connection, userId);
        if (membership is null)
        {
            return (null, "Only a home manager or co-manager can record payments.");
        }

        if (await IsFinalizedAsync(connection, null, membership.HomeId, year, month))
        {
            return (null, "This settlement period is finalized.");
        }

        var memberExists = await IsActiveMemberAsync(connection, null, membership.HomeId, request.UserId);
        if (!memberExists)
        {
            return (null, "The payment recipient is not an active member of this home.");
        }

        if (!TryNormalizeDate(request.PaidOn, year, month, out var paidOn))
        {
            return (null, "The payment date must belong to the selected settlement period.");
        }
        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO contributions (house_id, user_id, amount, paid_on, period_year,
                                       period_month, source, fund_type, recorded_by_user_id)
            VALUES (@homeId, @memberId, @amount, @paidOn, @year, @month, 2,
                    @fundType, @recordedBy)
            RETURNING id
            """,
            new { homeId = membership.HomeId, memberId = request.UserId, amount = request.Amount,
                paidOn, year, month, fundType = request.FundType, recordedBy = userId });

        return (await connection.QuerySingleAsync<SettlementPaymentDto>(
            """
            SELECT c.id, c.user_id AS UserId, u.full_name AS MemberName, c.amount,
                   c.fund_type AS FundType, c.note AS Note, c.paid_on AS PaidOn
            FROM contributions c JOIN users u ON u.id = c.user_id
            WHERE c.id = @id
            """, new { id }), null);
    }

    public async Task<(bool Ok, string Message)> SaveMealsAsync(
        long userId, int year, int month, SaveSettlementMealsRequest request)
    {
        ValidatePeriod(year, month);
        if (request.Entries is null || request.Entries.Count == 0)
        {
            return (false, "No meal changes were supplied.");
        }

        using var connection = await _db.OpenAsync();
        var membership = await GetMembershipAsync(connection, null, userId);
        if (membership is null)
        {
            return (false, "You are not in a home.");
        }

        if (await IsFinalizedAsync(connection, null, membership.HomeId, year, month))
        {
            return (false, "This settlement period is finalized.");
        }

        var canEditOthers = membership.Role is HomeService.RoleManager or HomeService.RoleCoManager;
        foreach (var entry in request.Entries)
        {
            if (entry.MealCount is < 0m or > 10m || entry.MealDate.Year != year || entry.MealDate.Month != month)
            {
                return (false, "Meal dates and counts must belong to the selected period.");
            }

            if (!canEditOthers && entry.UserId != userId)
            {
                return (false, "Members can edit only their own meals.");
            }

            if (!await IsActiveMemberAsync(connection, null, membership.HomeId, entry.UserId))
            {
                return (false, "Every meal entry must belong to an active home member.");
            }
        }

        using var transaction = connection.BeginTransaction();
        foreach (var entry in request.Entries)
        {
            var previous = await connection.ExecuteScalarAsync<decimal?>(
                """
                SELECT meal_count
                FROM meal_entries
                WHERE house_id = @homeId AND user_id = @memberId AND meal_date = @mealDate
                ORDER BY recorded_at_utc DESC, id DESC
                LIMIT 1
                """, new { homeId = membership.HomeId, memberId = entry.UserId,
                    mealDate = entry.MealDate.Date }, transaction);

            var id = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO meal_entries (house_id, user_id, meal_date, meal_count, period_year,
                                          period_month, supersedes_meal_entry_id, recorded_by_user_id)
                SELECT @homeId, @memberId, @mealDate, @mealCount, @year, @month,
                       (SELECT id FROM meal_entries
                        WHERE house_id = @homeId AND user_id = @memberId AND meal_date = @mealDate
                        ORDER BY recorded_at_utc DESC, id DESC LIMIT 1),
                       @recordedBy
                RETURNING id
                """, new { homeId = membership.HomeId, memberId = entry.UserId,
                    mealDate = entry.MealDate.Date, mealCount = entry.MealCount,
                    year, month, recordedBy = userId }, transaction);

            await connection.ExecuteAsync(
                """
                INSERT INTO meal_entry_audits (meal_entry_id, house_id, target_user_id,
                                               actor_user_id, old_meal_count, new_meal_count, reason)
                VALUES (@id, @homeId, @memberId, @recordedBy, @previous, @mealCount,
                        'Settlement page edit')
                """, new { id, homeId = membership.HomeId, memberId = entry.UserId,
                    recordedBy = userId, previous, mealCount = entry.MealCount }, transaction);
        }

        transaction.Commit();
        return (true, "Meal sheet saved.");
    }

    public async Task<(SettlementResultDto? Result, string? Error)> FinalizeAsync(
        long userId, int year, int month)
    {
        ValidatePeriod(year, month);
        using var connection = await _db.OpenAsync();
        var membership = await GetManagerMembershipAsync(connection, userId);
        if (membership is null)
        {
            return (null, "Only a home manager or co-manager can finalize a settlement.");
        }

        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        if (await IsFinalizedAsync(connection, transaction, membership.HomeId, year, month))
        {
            transaction.Rollback();
            return (null, "This settlement period is already finalized.");
        }

        var workspace = await LoadWorkspaceAsync(connection, transaction, membership.HomeId, userId, year, month);
        var result = workspace.Result!;
        if (Math.Round(result.NetTotal, 2, MidpointRounding.AwayFromZero) != 0m)
        {
            transaction.Rollback();
            return (null, "Payments must equal the month's costs before finalization.");
        }

        var runId = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO settlement_runs (house_id, period_year, period_month,
                total_meal_spending, total_meals, per_meal_rate, total_equal_costs,
                member_count_at_settlement, status, computed_by_user_id)
            VALUES (@homeId, @year, @month, @mealFund, @totalMeals, @rate, @bills,
                    @memberCount, 2, @userId)
            RETURNING id
            """, new { homeId = membership.HomeId, year, month,
                mealFund = result.MealFund, totalMeals = result.TotalMeals,
                rate = result.PerMealRate, bills = result.BillsTotal,
                memberCount = result.Lines.Count, userId }, transaction);

        foreach (var line in result.Lines)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO settlement_lines (settlement_run_id, user_id, meal_count,
                    meal_cost, equal_share, contributions, rounding_adjustment, net_amount)
                VALUES (@runId, @userId, @meals, @mealCost, @equalShare, @contributions,
                        @adjustment, @net)
                """, new { runId, userId = line.UserId, meals = line.Meals,
                    mealCost = line.MealCost, equalShare = line.EqualShare,
                    contributions = line.Contributions, adjustment = line.RoundingAdjustment,
                    net = line.NetAmount }, transaction);
        }

        foreach (var transfer in result.Transfers)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO settlement_transfers (settlement_run_id, from_user_id, to_user_id, amount)
                VALUES (@runId, @fromUserId, @toUserId, @amount)
                """, new { runId, fromUserId = transfer.FromUserId,
                    toUserId = transfer.ToUserId, amount = transfer.Amount }, transaction);
        }

        transaction.Commit();
        workspace.IsFinalized = true;
        return (result, null);
    }

    private async Task<SettlementWorkspaceDto> LoadWorkspaceAsync(
        IDbConnection connection, IDbTransaction? transaction, long homeId, long userId, int year, int month)
    {
        var members = (await connection.QueryAsync<SettlementMemberDto>(
            """
            SELECT hm.user_id AS UserId, u.full_name AS Name, hm.role AS Role,
                   (hm.user_id = @userId) AS IsMe
            FROM home_members hm JOIN users u ON u.id = hm.user_id
            WHERE hm.home_id = @homeId AND hm.left_at_utc IS NULL
            ORDER BY hm.role, hm.joined_at_utc
            """, new { homeId, userId }, transaction)).ToList();

        var bills = (await connection.QueryAsync<SettlementBillDto>(
            """
            SELECT e.id, e.description, e.amount, e.spent_on AS SpentOn,
                   e.spent_by_user_id AS SpentByUserId, u.full_name AS SpentByName
            FROM expenses e JOIN users u ON u.id = e.spent_by_user_id
            WHERE e.house_id = @homeId AND e.period_year = @year
              AND e.period_month = @month AND e.category = 1
            ORDER BY e.spent_on DESC, e.id DESC
            """, new { homeId, year, month }, transaction)).ToList();

        var payments = (await connection.QueryAsync<SettlementPaymentDto>(
            """
            SELECT c.id, c.user_id AS UserId, u.full_name AS MemberName, c.amount,
                   c.fund_type AS FundType, c.note AS Note, c.paid_on AS PaidOn
            FROM contributions c JOIN users u ON u.id = c.user_id
            WHERE c.house_id = @homeId AND c.period_year = @year AND c.period_month = @month
            ORDER BY c.paid_on DESC, c.id DESC
            """, new { homeId, year, month }, transaction)).ToList();

        var meals = (await connection.QueryAsync<SettlementMealDto>(
            """
            SELECT x.user_id AS UserId, u.full_name AS MemberName, x.meal_date AS MealDate,
                   x.meal_count AS MealCount, x.recorded_at_utc AS RecordedAtUtc
            FROM (
                SELECT DISTINCT ON (user_id, meal_date) user_id, meal_date, meal_count, recorded_at_utc
                FROM meal_entries
                WHERE house_id = @homeId AND period_year = @year AND period_month = @month
                ORDER BY user_id, meal_date, recorded_at_utc DESC, id DESC
            ) x JOIN users u ON u.id = x.user_id
            ORDER BY x.meal_date, x.user_id
            """, new { homeId, year, month }, transaction)).ToList();

        var isFinalized = await IsFinalizedAsync(connection, transaction, homeId, year, month);
        var result = CalculateResult(members, bills, payments, meals);
        return new SettlementWorkspaceDto
        {
            Year = year,
            Month = month,
            IsFinalized = isFinalized,
            BillsTotal = result.BillsTotal,
            MealFundTotal = result.MealFund,
            SharedFundTotal = payments.Where(p => p.FundType == SharedBills).Sum(p => p.Amount),
            TotalMeals = result.TotalMeals,
            PerMealRate = result.PerMealRate,
            BillShare = result.BillShare,
            OutstandingBills = result.BillsTotal - payments.Where(p => p.FundType == SharedBills).Sum(p => p.Amount),
            Members = members,
            Bills = bills,
            Payments = payments,
            Meals = meals,
            Result = result
        };
    }

    private static SettlementResultDto CalculateResult(
        List<SettlementMemberDto> members, List<SettlementBillDto> bills,
        List<SettlementPaymentDto> payments, List<SettlementMealDto> meals)
    {
        var billsTotal = bills.Sum(b => b.Amount);
        var mealFund = payments.Where(p => p.FundType == MealFund).Sum(p => p.Amount);
        var totalMeals = meals.Sum(m => m.MealCount);
        var rate = totalMeals == 0m ? 0m : Math.Round(mealFund / totalMeals, 6, MidpointRounding.AwayFromZero);
        var billShare = members.Count == 0 ? 0m : Math.Round(billsTotal / members.Count, 2, MidpointRounding.AwayFromZero);

        var lines = members.Select(member =>
        {
            var memberMeals = meals.Where(m => m.UserId == member.UserId).Sum(m => m.MealCount);
            var mealCost = Math.Round(memberMeals * rate, 2, MidpointRounding.AwayFromZero);
            var contributions = payments.Where(p => p.UserId == member.UserId).Sum(p => p.Amount);
            return new SettlementLineDto
            {
                UserId = member.UserId, MemberName = member.Name, Meals = memberMeals,
                MealCost = mealCost, EqualShare = billShare, Contributions = contributions,
                NetAmount = Math.Round(contributions - mealCost - billShare, 2, MidpointRounding.AwayFromZero)
            };
        }).ToList();

        var residual = mealFund - lines.Sum(line => line.MealCost);
        if (residual != 0m && lines.Count > 0)
        {
            var line = lines.OrderByDescending(l => l.MealCost).ThenBy(l => l.UserId).First();
            line.MealCost += residual;
            line.RoundingAdjustment = residual;
            line.NetAmount = Math.Round(line.Contributions - line.MealCost - line.EqualShare, 2, MidpointRounding.AwayFromZero);
        }

        var transfers = BuildTransfers(lines);
        return new SettlementResultDto
        {
            BillsTotal = billsTotal, MealFund = mealFund, TotalMeals = totalMeals,
            PerMealRate = rate, BillShare = billShare, NetTotal = lines.Sum(l => l.NetAmount),
            Lines = lines, Transfers = transfers
        };
    }

    private static List<SettlementTransferDto> BuildTransfers(List<SettlementLineDto> lines)
    {
        var creditors = lines.Where(l => l.NetAmount > 0m)
            .OrderByDescending(l => l.NetAmount).Select(l => new TransferBalance(l)).ToList();
        var debtors = lines.Where(l => l.NetAmount < 0m)
            .OrderByDescending(l => Math.Abs(l.NetAmount)).Select(l => new TransferBalance(l)).ToList();
        var transfers = new List<SettlementTransferDto>();
        var creditorIndex = 0;
        var debtorIndex = 0;

        while (creditorIndex < creditors.Count && debtorIndex < debtors.Count)
        {
            var creditor = creditors[creditorIndex];
            var debtor = debtors[debtorIndex];
            var amount = Math.Min(creditor.Remaining, debtor.Remaining);
            transfers.Add(new SettlementTransferDto
            {
                FromUserId = debtor.Line.UserId, FromMemberName = debtor.Line.MemberName,
                ToUserId = creditor.Line.UserId, ToMemberName = creditor.Line.MemberName,
                Amount = amount
            });
            creditor.Remaining -= amount;
            debtor.Remaining -= amount;
            if (creditor.Remaining == 0m) creditorIndex++;
            if (debtor.Remaining == 0m) debtorIndex++;
        }

        return transfers;
    }

    private static async Task<Membership?> GetMembershipAsync(IDbConnection connection, IDbTransaction? transaction, long userId) =>
        await connection.QuerySingleOrDefaultAsync<Membership>(
            "SELECT home_id AS HomeId, role AS Role FROM home_members WHERE user_id = @userId AND left_at_utc IS NULL",
            new { userId }, transaction);

    private static async Task<Membership?> GetManagerMembershipAsync(IDbConnection connection, long userId)
    {
        var membership = await GetMembershipAsync(connection, null, userId);
        return membership?.Role is HomeService.RoleManager or HomeService.RoleCoManager ? membership : null;
    }

    private static Task<bool> IsActiveMemberAsync(IDbConnection connection, IDbTransaction? transaction, long homeId, long userId) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM home_members WHERE home_id = @homeId AND user_id = @userId AND left_at_utc IS NULL)",
            new { homeId, userId }, transaction);

    private static Task<bool> IsFinalizedAsync(IDbConnection connection, IDbTransaction? transaction, long homeId, int year, int month) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM settlement_runs WHERE house_id = @homeId AND period_year = @year AND period_month = @month AND status = 2)",
            new { homeId, year, month }, transaction);

    private static bool TryNormalizeDate(DateTime value, int year, int month, out DateTime date)
    {
        date = value.Date;
        return date.Year == year && date.Month == month;
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2020 or > 2100 || month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Invalid settlement period.");
        }
    }

    private sealed class Membership
    {
        public long HomeId { get; set; }
        public short Role { get; set; }
    }

    private sealed class TransferBalance
    {
        public TransferBalance(SettlementLineDto line)
        {
            Line = line;
            Remaining = Math.Abs(line.NetAmount);
        }

        public SettlementLineDto Line { get; }
        public decimal Remaining { get; set; }
    }
}

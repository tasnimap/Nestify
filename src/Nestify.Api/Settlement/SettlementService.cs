using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Api.Homes;
using Nestify.Shared.Dtos.Settlement;

namespace Nestify.Api.Settlement;

public sealed class SettlementService
{
    private const short EqualSplit = 1;
    private const short MealFund = 1;
    private const short SharedBills = 2;
    private const short BookNone = 0;
    private const short BookOpen = 1;
    private const short BookFinalized = 2;

    // Every book starts with these lines at 0 so the manager only fills in amounts.
    private static readonly string[] DefaultBills = ["Rent", "Electricity", "Water", "Gas", "Internet"];

    private readonly DbConnectionFactory _db;

    public SettlementService(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task EnsureSchemaCompatibilityAsync()
    {
        using var connection = await _db.OpenAsync();
        await connection.ExecuteAsync(
            """
            ALTER TABLE settlement_runs
                ADD COLUMN IF NOT EXISTS opened_by_user_id bigint REFERENCES users (id) ON DELETE RESTRICT;
            ALTER TABLE settlement_runs
                ADD COLUMN IF NOT EXISTS opened_at_utc timestamptz NOT NULL DEFAULT now();
            ALTER TABLE settlement_runs
                ALTER COLUMN total_meal_spending SET DEFAULT 0,
                ALTER COLUMN total_meals SET DEFAULT 0,
                ALTER COLUMN per_meal_rate SET DEFAULT 0,
                ALTER COLUMN total_equal_costs SET DEFAULT 0,
                ALTER COLUMN member_count_at_settlement SET DEFAULT 0,
                ALTER COLUMN computed_by_user_id DROP NOT NULL,
                ALTER COLUMN computed_at_utc SET DEFAULT now();
            ALTER TABLE meal_entries
                ADD COLUMN IF NOT EXISTS breakfast numeric(4,1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS lunch numeric(4,1) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS dinner numeric(4,1) NOT NULL DEFAULT 0;
            UPDATE meal_entries
            SET breakfast = meal_count
            WHERE breakfast = 0 AND lunch = 0 AND dinner = 0 AND meal_count <> 0;
            ALTER TABLE meal_entries DROP CONSTRAINT IF EXISTS ck_meal_count;
            ALTER TABLE meal_entries
                ADD CONSTRAINT ck_meal_count CHECK (meal_count BETWEEN 0 AND 30);
            INSERT INTO home_members (home_id, user_id, role)
            SELECT h.id, h.created_by_user_id, 1
            FROM homes h
            WHERE NOT EXISTS (
                    SELECT 1 FROM home_members hm
                    WHERE hm.home_id = h.id
                        AND hm.user_id = h.created_by_user_id
            )
            AND NOT EXISTS (
                    SELECT 1 FROM home_members hm
                    WHERE hm.user_id = h.created_by_user_id
                        AND hm.left_at_utc IS NULL
            )
            AND NOT EXISTS (
                    SELECT 1 FROM home_members hm
                    WHERE hm.home_id = h.id
                        AND hm.role = 1
                        AND hm.left_at_utc IS NULL
            );
            """);
    }

    public async Task<List<SettlementBookDto>?> GetBooksAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var membership = await GetMembershipAsync(connection, null, userId);
        if (membership is null)
        {
            return null;
        }

        return (await connection.QueryAsync<SettlementBookDto>(
            """
            SELECT period_year AS Year, period_month AS Month, status AS Status
            FROM settlement_runs
            WHERE house_id = @homeId
            ORDER BY period_year, period_month
            """, new { homeId = membership.HomeId })).ToList();
    }

    public async Task<List<MonthlyMemberMealCostDto>?> GetMealCostHistoryAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var membership = await GetMembershipAsync(connection, null, userId);
        if (membership is null)
        {
            return null;
        }

        return (await connection.QueryAsync<MonthlyMemberMealCostDto>(
            """
            WITH current_meals AS (
                SELECT DISTINCT ON (house_id, user_id, meal_date)
                       house_id, user_id, meal_date, meal_count
                FROM meal_entries
                WHERE house_id = @homeId
                ORDER BY house_id, user_id, meal_date, recorded_at_utc DESC, id DESC
            ), meal_totals AS (
                SELECT house_id, user_id,
                       EXTRACT(YEAR FROM meal_date)::int AS year,
                       EXTRACT(MONTH FROM meal_date)::int AS month,
                       SUM(meal_count) AS meal_count
                FROM current_meals
                GROUP BY house_id, user_id, year, month
            ), rates AS (
                SELECT r.id, r.period_year AS year, r.period_month AS month,
                       COALESCE(NULLIF(r.per_meal_rate, 0),
                           COALESCE((SELECT SUM(c.amount) FROM contributions c
                                     WHERE c.house_id = r.house_id AND c.period_year = r.period_year
                                       AND c.period_month = r.period_month AND c.fund_type = 1), 0)
                           / NULLIF((SELECT SUM(mt.meal_count) FROM meal_totals mt
                                     WHERE mt.house_id = r.house_id AND mt.year = r.period_year
                                       AND mt.month = r.period_month), 0), 0) AS per_meal_rate,
                       r.house_id
                FROM settlement_runs r
                WHERE r.house_id = @homeId
            )
            SELECT sm.user_id AS UserId, u.full_name AS MemberName,
                   rates.year AS Year, rates.month AS Month,
                   COALESCE(mt.meal_count, 0) AS MealCount,
                   rates.per_meal_rate AS PerMealRate,
                   ROUND(COALESCE(mt.meal_count, 0) * rates.per_meal_rate, 2) AS MealCost
            FROM rates
            JOIN settlement_members sm ON sm.settlement_run_id = rates.id
            JOIN users u ON u.id = sm.user_id
            LEFT JOIN meal_totals mt ON mt.house_id = rates.house_id AND mt.user_id = sm.user_id
                AND mt.year = rates.year AND mt.month = rates.month
            ORDER BY rates.year, rates.month, u.full_name
            """, new { homeId = membership.HomeId })).ToList();
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

        return await LoadWorkspaceAsync(connection, null, membership, userId, year, month);
    }

    public async Task<(bool Ok, string Message)> OpenBookAsync(long userId, int year, int month)
    {
        ValidatePeriod(year, month);
        using var connection = await _db.OpenAsync();
        var membership = await GetManagerMembershipAsync(connection, userId);
        if (membership is null)
        {
            return (false, "Only a home manager or co-manager can open the book.");
        }

        using var transaction = connection.BeginTransaction();
        if (await GetBookAsync(connection, transaction, membership.HomeId, year, month) is not null)
        {
            transaction.Rollback();
            return (false, "A book already exists for this month.");
        }

        var openExists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM settlement_runs WHERE house_id = @homeId AND status = 1)",
            new { homeId = membership.HomeId }, transaction);
        if (openExists)
        {
            transaction.Rollback();
            return (false, "Finalize the open book before opening another month.");
        }

        var runId = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO settlement_runs (house_id, period_year, period_month, status, opened_by_user_id)
            VALUES (@homeId, @year, @month, 1, @userId)
            RETURNING id
            """, new { homeId = membership.HomeId, year, month, userId }, transaction);

        await connection.ExecuteAsync(
            """
            INSERT INTO settlement_members (settlement_run_id, user_id, added_by_user_id)
            SELECT @runId, user_id, @userId
            FROM home_members
            WHERE home_id = @homeId AND left_at_utc IS NULL
            """, new { runId, homeId = membership.HomeId, userId }, transaction);

        var firstDay = new DateTime(year, month, 1);
        foreach (var name in DefaultBills)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO expenses (house_id, category, description, amount, spent_by_user_id,
                                      spent_on, period_year, period_month, created_by_user_id)
                VALUES (@homeId, @category, @name, 0, @userId, @firstDay, @year, @month, @userId)
                """, new { homeId = membership.HomeId, category = EqualSplit, name, userId, firstDay, year, month },
                transaction);
        }

        transaction.Commit();
        return (true, "The book is open.");
    }

    public async Task<(bool Ok, string Message)> AddMemberAsync(
        long userId, int year, int month, AddSettlementMemberRequest request)
    {
        ValidatePeriod(year, month);
        using var connection = await _db.OpenAsync();
        var membership = await GetManagerMembershipAsync(connection, userId);
        if (membership is null)
        {
            return (false, "Only a home manager or co-manager can add members to the book.");
        }

        var book = await GetOpenBookAsync(connection, null, membership.HomeId, year, month);
        if (book is null)
        {
            return (false, "This month's book is not open.");
        }

        if (!await IsActiveMemberAsync(connection, null, membership.HomeId, request.UserId))
        {
            return (false, "That person is not a member of this home.");
        }

        if (await IsBookMemberAsync(connection, null, book.Id, request.UserId))
        {
            return (false, "That member is already in the book.");
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO settlement_members (settlement_run_id, user_id, added_by_user_id)
            VALUES (@runId, @memberId, @userId)
            """, new { runId = book.Id, memberId = request.UserId, userId });
        return (true, "Member added to the book.");
    }

    public async Task<(SettlementBillDto? Data, string? Error)> AddBillAsync(
        long userId, int year, int month, CreateSettlementBillRequest request)
    {
        ValidatePeriod(year, month);
        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length is < 1 or > 200 || request.Amount < 0m)
        {
            return (null, "A bill description and a non-negative amount are required.");
        }

        using var connection = await _db.OpenAsync();
        var membership = await GetManagerMembershipAsync(connection, userId);
        if (membership is null)
        {
            return (null, "Only a home manager or co-manager can add bills.");
        }

        if (await GetOpenBookAsync(connection, null, membership.HomeId, year, month) is null)
        {
            return (null, "This month's book is not open.");
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

    public async Task<(bool Ok, string Message)> UpdateBillAsync(
        long userId, int year, int month, long billId, UpdateSettlementBillRequest request)
    {
        ValidatePeriod(year, month);
        if (request.Amount < 0m)
        {
            return (false, "A bill amount cannot be negative.");
        }

        using var connection = await _db.OpenAsync();
        var membership = await GetManagerMembershipAsync(connection, userId);
        if (membership is null)
        {
            return (false, "Only a home manager or co-manager can change bills.");
        }

        if (await GetOpenBookAsync(connection, null, membership.HomeId, year, month) is null)
        {
            return (false, "This month's book is not open.");
        }

        var rows = await connection.ExecuteAsync(
            """
            UPDATE expenses SET amount = @amount
            WHERE id = @billId AND house_id = @homeId AND category = @category
              AND period_year = @year AND period_month = @month
            """, new { amount = request.Amount, billId, homeId = membership.HomeId, category = EqualSplit, year, month });
        return rows == 0 ? (false, "Bill not found.") : (true, "Bill updated.");
    }

    public async Task<(bool Ok, string Message)> DeleteBillAsync(long userId, int year, int month, long billId)
    {
        ValidatePeriod(year, month);
        using var connection = await _db.OpenAsync();
        var membership = await GetManagerMembershipAsync(connection, userId);
        if (membership is null)
        {
            return (false, "Only a home manager or co-manager can remove bills.");
        }

        if (await GetOpenBookAsync(connection, null, membership.HomeId, year, month) is null)
        {
            return (false, "This month's book is not open.");
        }

        var rows = await connection.ExecuteAsync(
            """
            DELETE FROM expenses
            WHERE id = @billId AND house_id = @homeId AND category = @category
              AND period_year = @year AND period_month = @month
            """, new { billId, homeId = membership.HomeId, category = EqualSplit, year, month });
        return rows == 0 ? (false, "Bill not found.") : (true, "Bill removed.");
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

        var book = await GetOpenBookAsync(connection, null, membership.HomeId, year, month);
        if (book is null)
        {
            return (null, "This month's book is not open.");
        }

        if (!await IsBookMemberAsync(connection, null, book.Id, request.UserId))
        {
            return (null, "The payer is not in this month's book.");
        }

        if (!TryNormalizeDate(request.PaidOn, year, month, out var paidOn))
        {
            return (null, "The payment date must belong to the selected settlement period.");
        }
        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO contributions (house_id, user_id, amount, paid_on, period_year,
                                       period_month, source, fund_type, note, recorded_by_user_id)
            VALUES (@homeId, @memberId, @amount, @paidOn, @year, @month, 2,
                    @fundType, @note, @recordedBy)
            RETURNING id
            """,
            new { homeId = membership.HomeId, memberId = request.UserId, amount = request.Amount,
                paidOn, year, month, fundType = request.FundType, note, recordedBy = userId });

        return (await connection.QuerySingleAsync<SettlementPaymentDto>(
            """
            SELECT c.id, c.user_id AS UserId, u.full_name AS MemberName, c.amount,
                   c.fund_type AS FundType, c.note AS Note, c.paid_on AS PaidOn
            FROM contributions c JOIN users u ON u.id = c.user_id
            WHERE c.id = @id
            """, new { id }), null);
    }

    public async Task<(bool Ok, string Message)> DeletePaymentAsync(long userId, int year, int month, long paymentId)
    {
        ValidatePeriod(year, month);
        using var connection = await _db.OpenAsync();
        var membership = await GetManagerMembershipAsync(connection, userId);
        if (membership is null)
        {
            return (false, "Only a home manager or co-manager can remove payments.");
        }

        if (await GetOpenBookAsync(connection, null, membership.HomeId, year, month) is null)
        {
            return (false, "This month's book is not open.");
        }

        var rows = await connection.ExecuteAsync(
            """
            DELETE FROM contributions
            WHERE id = @paymentId AND house_id = @homeId AND period_year = @year AND period_month = @month
            """, new { paymentId, homeId = membership.HomeId, year, month });
        return rows == 0 ? (false, "Payment not found.") : (true, "Payment removed.");
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

        var book = await GetOpenBookAsync(connection, null, membership.HomeId, year, month);
        if (book is null)
        {
            return (false, "This month's book is not open.");
        }

        var canEditOthers = membership.Role is HomeService.RoleManager or HomeService.RoleCoManager;
        foreach (var entry in request.Entries)
        {
            if (entry.Breakfast is < 0m or > 10m || entry.Lunch is < 0m or > 10m || entry.Dinner is < 0m or > 10m
                || entry.MealDate.Year != year || entry.MealDate.Month != month)
            {
                return (false, "Meal dates and counts must belong to the selected period.");
            }

            if (!canEditOthers && entry.UserId != userId)
            {
                return (false, "Members can edit only their own meals.");
            }

            if (!await IsBookMemberAsync(connection, null, book.Id, entry.UserId))
            {
                return (false, "Every meal entry must belong to a member of this month's book.");
            }
        }

        using var transaction = connection.BeginTransaction();
        foreach (var entry in request.Entries)
        {
            var mealCount = entry.Breakfast + entry.Lunch + entry.Dinner;
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
                INSERT INTO meal_entries (house_id, user_id, meal_date, breakfast, lunch, dinner, meal_count,
                                          period_year, period_month, supersedes_meal_entry_id, recorded_by_user_id)
                SELECT @homeId, @memberId, @mealDate, @breakfast, @lunch, @dinner, @mealCount, @year, @month,
                       (SELECT id FROM meal_entries
                        WHERE house_id = @homeId AND user_id = @memberId AND meal_date = @mealDate
                        ORDER BY recorded_at_utc DESC, id DESC LIMIT 1),
                       @recordedBy
                RETURNING id
                """, new { homeId = membership.HomeId, memberId = entry.UserId,
                    mealDate = entry.MealDate.Date, breakfast = entry.Breakfast, lunch = entry.Lunch,
                    dinner = entry.Dinner, mealCount, year, month, recordedBy = userId }, transaction);

            await connection.ExecuteAsync(
                """
                INSERT INTO meal_entry_audits (meal_entry_id, house_id, target_user_id,
                                               actor_user_id, old_meal_count, new_meal_count, reason)
                VALUES (@id, @homeId, @memberId, @recordedBy, @previous, @mealCount,
                        'Settlement page edit')
                """, new { id, homeId = membership.HomeId, memberId = entry.UserId,
                    recordedBy = userId, previous, mealCount }, transaction);
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
        var book = await GetOpenBookAsync(connection, transaction, membership.HomeId, year, month);
        if (book is null)
        {
            transaction.Rollback();
            return (null, "This month's book is not open.");
        }

        var workspace = await LoadWorkspaceAsync(connection, transaction, membership, userId, year, month);
        var result = workspace.Result!;
        if (Math.Round(result.NetTotal, 2, MidpointRounding.AwayFromZero) != 0m)
        {
            transaction.Rollback();
            return (null, "Payments must equal the month's costs before finalization.");
        }

        await connection.ExecuteAsync(
            """
            UPDATE settlement_runs
            SET status = 2, total_meal_spending = @mealFund, total_meals = @totalMeals,
                per_meal_rate = @rate, total_equal_costs = @bills,
                member_count_at_settlement = @memberCount,
                computed_by_user_id = @userId, computed_at_utc = now()
            WHERE id = @runId
            """, new { runId = book.Id, mealFund = result.MealFund, totalMeals = result.TotalMeals,
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
                """, new { runId = book.Id, userId = line.UserId, meals = line.Meals,
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
                """, new { runId = book.Id, fromUserId = transfer.FromUserId,
                    toUserId = transfer.ToUserId, amount = transfer.Amount }, transaction);
        }

        transaction.Commit();
        return (result, null);
    }

    private async Task<SettlementWorkspaceDto> LoadWorkspaceAsync(
        IDbConnection connection, IDbTransaction? transaction, Membership membership, long userId, int year, int month)
    {
        var homeId = membership.HomeId;
        var canManage = membership.Role is HomeService.RoleManager or HomeService.RoleCoManager;
        var book = await GetBookAsync(connection, transaction, homeId, year, month);

        var activeMembers = (await connection.QueryAsync<SettlementMemberDto>(
            """
            SELECT hm.user_id AS UserId, u.full_name AS Name, hm.role AS Role,
                   (hm.user_id = @userId) AS IsMe, true AS IsActive
            FROM home_members hm JOIN users u ON u.id = hm.user_id
            WHERE hm.home_id = @homeId AND hm.left_at_utc IS NULL
            ORDER BY hm.role, hm.joined_at_utc
            """, new { homeId, userId }, transaction)).ToList();

        if (book is null)
        {
            return new SettlementWorkspaceDto
            {
                Year = year,
                Month = month,
                BookStatus = BookNone,
                CanManage = canManage,
                AddableMembers = activeMembers,
                Result = new SettlementResultDto()
            };
        }

        var members = (await connection.QueryAsync<SettlementMemberDto>(
            """
            SELECT sm.user_id AS UserId, u.full_name AS Name,
                   COALESCE(hm.role, 3) AS Role,
                   (sm.user_id = @userId) AS IsMe,
                   (hm.id IS NOT NULL) AS IsActive
            FROM settlement_members sm
            JOIN users u ON u.id = sm.user_id
            LEFT JOIN home_members hm ON hm.user_id = sm.user_id AND hm.home_id = @homeId AND hm.left_at_utc IS NULL
            WHERE sm.settlement_run_id = @runId
            ORDER BY COALESCE(hm.role, 3), sm.added_at_utc
            """, new { homeId, userId, runId = book.Id }, transaction)).ToList();

        var bookUserIds = members.Select(m => m.UserId).ToHashSet();
        var addable = activeMembers.Where(m => !bookUserIds.Contains(m.UserId)).ToList();

        var bills = (await connection.QueryAsync<SettlementBillDto>(
            """
            SELECT e.id, e.description, e.amount, e.spent_on AS SpentOn,
                   e.spent_by_user_id AS SpentByUserId, u.full_name AS SpentByName
            FROM expenses e JOIN users u ON u.id = e.spent_by_user_id
            WHERE e.house_id = @homeId AND e.period_year = @year
              AND e.period_month = @month AND e.category = 1
            ORDER BY e.id
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
                   x.breakfast AS Breakfast, x.lunch AS Lunch, x.dinner AS Dinner,
                   x.meal_count AS MealCount, x.recorded_at_utc AS RecordedAtUtc
            FROM (
                SELECT DISTINCT ON (user_id, meal_date) user_id, meal_date, breakfast, lunch, dinner, meal_count, recorded_at_utc
                FROM meal_entries
                WHERE house_id = @homeId AND period_year = @year AND period_month = @month
                ORDER BY user_id, meal_date, recorded_at_utc DESC, id DESC
            ) x JOIN users u ON u.id = x.user_id
            WHERE x.user_id = ANY(@userIds)
            ORDER BY x.meal_date, x.user_id
            """, new { homeId, year, month, userIds = bookUserIds.ToArray() }, transaction)).ToList();

        var sharedFund = payments.Where(p => p.FundType == SharedBills).Sum(p => p.Amount);
        var result = CalculateResult(members, bills, payments, meals);
        return new SettlementWorkspaceDto
        {
            Year = year,
            Month = month,
            BookStatus = book.Status,
            IsFinalized = book.Status == BookFinalized,
            CanManage = canManage,
            BillsTotal = result.BillsTotal,
            MealFundTotal = result.MealFund,
            SharedFundTotal = sharedFund,
            TotalMeals = result.TotalMeals,
            PerMealRate = result.PerMealRate,
            BillShare = result.BillShare,
            OutstandingBills = result.BillsTotal - sharedFund,
            Members = members,
            AddableMembers = addable,
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

    private static async Task<Membership?> GetMembershipAsync(IDbConnection connection, IDbTransaction? transaction, long userId)
    {
        var membership = await connection.QuerySingleOrDefaultAsync<Membership>(
            "SELECT home_id AS HomeId, role AS Role FROM home_members WHERE user_id = @userId AND left_at_utc IS NULL",
            new { userId }, transaction);

        if (membership is not null || transaction is not null)
        {
            return membership;
        }

        return await RepairLegacyCreatorMembershipAsync(connection, userId);
    }

    private static async Task<Membership?> RepairLegacyCreatorMembershipAsync(IDbConnection connection, long userId)
    {
        var homeId = await connection.ExecuteScalarAsync<long?>(
            """
            SELECT h.id
            FROM homes h
            WHERE h.created_by_user_id = @userId
              AND NOT EXISTS (
                    SELECT 1 FROM home_members hm
                    WHERE hm.home_id = h.id AND hm.user_id = @userId
              )
              AND NOT EXISTS (
                    SELECT 1 FROM home_members hm
                    WHERE hm.user_id = @userId AND hm.left_at_utc IS NULL
              )
              AND NOT EXISTS (
                    SELECT 1 FROM home_members hm
                    WHERE hm.home_id = h.id AND hm.role = @managerRole AND hm.left_at_utc IS NULL
              )
            ORDER BY h.created_at_utc DESC
            LIMIT 1
            """, new { userId, managerRole = HomeService.RoleManager });

        if (homeId is null)
        {
            return null;
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO home_members (home_id, user_id, role)
            VALUES (@homeId, @userId, @managerRole)
            """, new { homeId, userId, managerRole = HomeService.RoleManager });

        return new Membership
        {
            HomeId = homeId.Value,
            Role = HomeService.RoleManager
        };
    }

    private static async Task<Membership?> GetManagerMembershipAsync(IDbConnection connection, long userId)
    {
        var membership = await GetMembershipAsync(connection, null, userId);
        return membership?.Role is HomeService.RoleManager or HomeService.RoleCoManager ? membership : null;
    }

    private static Task<bool> IsActiveMemberAsync(IDbConnection connection, IDbTransaction? transaction, long homeId, long userId) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM home_members WHERE home_id = @homeId AND user_id = @userId AND left_at_utc IS NULL)",
            new { homeId, userId }, transaction);

    private static Task<bool> IsBookMemberAsync(IDbConnection connection, IDbTransaction? transaction, long runId, long userId) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM settlement_members WHERE settlement_run_id = @runId AND user_id = @userId)",
            new { runId, userId }, transaction);

    private static Task<Book?> GetBookAsync(IDbConnection connection, IDbTransaction? transaction, long homeId, int year, int month) =>
        connection.QuerySingleOrDefaultAsync<Book>(
            "SELECT id AS Id, status AS Status FROM settlement_runs WHERE house_id = @homeId AND period_year = @year AND period_month = @month",
            new { homeId, year, month }, transaction);

    private static async Task<Book?> GetOpenBookAsync(IDbConnection connection, IDbTransaction? transaction, long homeId, int year, int month)
    {
        var book = await GetBookAsync(connection, transaction, homeId, year, month);
        return book?.Status == BookOpen ? book : null;
    }

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

    private sealed class Book
    {
        public long Id { get; set; }
        public short Status { get; set; }
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

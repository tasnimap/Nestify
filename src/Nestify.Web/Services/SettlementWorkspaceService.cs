namespace Nestify.Web.Services;

public sealed class SettlementWorkspaceService
{
    public const int PeriodYear = 2026;
    public const int PeriodMonth = 9;

    private static readonly string[] Members = ["Rafi", "Sadia", "Tanvir", "Nabil"];
    private readonly List<ExpenseEntry> _expenses = [];
    private readonly List<ContributionEntry> _contributions = [];
    private readonly Dictionary<MealCellKey, MealCell> _mealCells = [];
    private readonly DateTime[] _days;

    public SettlementWorkspaceService()
    {
        _days = Enumerable.Range(1, DateTime.DaysInMonth(PeriodYear, PeriodMonth))
            .Select(day => CreatePeriodDateUtc(day))
            .ToArray();

        SeedExpenses();
        SeedMeals();
    }

    public IReadOnlyList<string> MemberNames => Members;
    public IReadOnlyList<DateTime> Days => _days;
    public IReadOnlyList<ExpenseEntry> Expenses => _expenses;
    public IReadOnlyList<ContributionEntry> Contributions => _contributions;

    public bool IsFinalized { get; private set; }

    public decimal TotalExpenses => _expenses.Sum(item => item.Amount);
    public decimal TotalContributions => _contributions.Sum(item => item.Amount);

    public static DateTime CreatePeriodDateUtc(int day) =>
        new(PeriodYear, PeriodMonth, day, 12, 0, 0, DateTimeKind.Utc);

    public static DateTime ToSettlementDateUtc(DateTime date) =>
        new(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Utc);

    public static DateTime GetDefaultFormDate()
    {
        var today = DateTime.UtcNow;
        if (today.Year == PeriodYear && today.Month == PeriodMonth)
        {
            return new DateTime(PeriodYear, PeriodMonth, today.Day);
        }

        return new DateTime(PeriodYear, PeriodMonth, 1);
    }

    public ExpenseEntry AddExpense(string description, decimal amount, string paidBy, string category, DateTime date)
    {
        var entry = new ExpenseEntry
        {
            Description = description.Trim(),
            Amount = amount,
            PaidBy = paidBy,
            Category = category,
            DateUtc = ToSettlementDateUtc(date)
        };

        _expenses.Insert(0, entry);
        _contributions.Insert(0, ContributionEntry.FromExpense(entry));
        return entry;
    }

    public ContributionEntry AddContribution(string member, decimal amount, string note, DateTime paidOn)
    {
        var entry = new ContributionEntry
        {
            Member = member,
            Amount = amount,
            Source = "Direct cash",
            Note = note.Trim(),
            PaidOnUtc = ToSettlementDateUtc(paidOn)
        };

        _contributions.Insert(0, entry);
        return entry;
    }

    public decimal GetContributionTotal(string member) => _contributions
        .Where(item => item.Member == member)
        .Sum(item => item.Amount);

    public MealCell GetMealCell(MealCellKey key) => _mealCells[key];

    public void ChangeMealCell(MealCellKey key, decimal value)
    {
        if (IsFinalized)
        {
            return;
        }

        var cell = _mealCells[key];
        cell.Value = Math.Clamp(value, 0m, 10m);
        cell.IsDirty = true;
    }

    public decimal GetDayMealTotal(DateTime day) => Members
        .Sum(member => GetMealCell(new MealCellKey(DateOnly.FromDateTime(day), member)).Value);

    public decimal GetMemberMealTotal(string member) => _days
        .Sum(day => GetMealCell(new MealCellKey(DateOnly.FromDateTime(day), member)).Value);

    public decimal TotalMeals => _mealCells.Values.Sum(cell => cell.Value);

    public void MarkMealsSaved()
    {
        foreach (var cell in _mealCells.Values.Where(cell => cell.IsDirty))
        {
            cell.RowVersion++;
            cell.IsDirty = false;
        }
    }

    public void MarkMealConflictServerVersion(MealCellKey key)
    {
        _mealCells[key].RowVersion++;
    }

    public SettlementResult CalculateSettlement()
    {
        var mealSpending = _expenses
            .Where(item => item.Category == "Meal based")
            .Sum(item => item.Amount);

        var equalSplitCosts = _expenses
            .Where(item => item.Category != "Meal based")
            .Sum(item => item.Amount);

        var totalMeals = TotalMeals;
        var perMealRate = totalMeals == 0m
            ? 0m
            : Math.Round(mealSpending / totalMeals, 6, MidpointRounding.AwayFromZero);

        var equalShare = Members.Length == 0
            ? 0m
            : Math.Round(equalSplitCosts / Members.Length, 2, MidpointRounding.AwayFromZero);

        var lines = Members
            .Select(member =>
            {
                var meals = GetMemberMealTotal(member);
                var mealCost = Math.Round(meals * perMealRate, 2, MidpointRounding.AwayFromZero);
                return new SettlementLine(
                    member,
                    meals,
                    mealCost,
                    equalShare,
                    GetContributionTotal(member),
                    0m,
                    0m);
            })
            .ToList();

        var residual = mealSpending - lines.Sum(line => line.MealCost);
        if (residual != 0m && lines.Count > 0)
        {
            var residualLineIndex = lines
                .Select((line, index) => new { line, index })
                .OrderByDescending(item => item.line.MealCost)
                .ThenBy(item => item.line.Member, StringComparer.Ordinal)
                .First()
                .index;

            var line = lines[residualLineIndex];
            lines[residualLineIndex] = line with
            {
                MealCost = line.MealCost + residual,
                RoundingAdjustment = residual
            };
        }

        lines = lines
            .Select(line => line with
            {
                Net = Math.Round(line.Contributions - line.MealCost - line.EqualShare, 2, MidpointRounding.AwayFromZero)
            })
            .ToList();

        var transfers = CalculateTransfers(lines);
        return new SettlementResult(mealSpending, equalSplitCosts, totalMeals, perMealRate, lines, transfers);
    }

    public void FinalizePeriod()
    {
        IsFinalized = true;
    }

    private void SeedExpenses()
    {
        AddExpense("Gas cylinder refill", 1400.00m, "Rafi", "Equal split", CreatePeriodDateUtc(3));
        AddExpense("Light bulbs + wiring", 600.00m, "Sadia", "Equal split", CreatePeriodDateUtc(7));
        AddExpense("Internet bill", 1150.00m, "Tanvir", "Equal split", CreatePeriodDateUtc(10));
        AddExpense("Groceries (1-10 Sep)", 4500.00m, "Rafi", "Meal based", CreatePeriodDateUtc(12));
        AddExpense("Groceries (11-20 Sep)", 2800.00m, "Sadia", "Meal based", CreatePeriodDateUtc(20));
        AddExpense("Rice and fish market", 1900.00m, "Nabil", "Meal based", CreatePeriodDateUtc(27));
    }

    private void SeedMeals()
    {
        foreach (var day in _days)
        {
            foreach (var member in Members)
            {
                _mealCells[new MealCellKey(DateOnly.FromDateTime(day), member)] = new MealCell
                {
                    Value = GetSeedMealCount(day.Day, member),
                    RowVersion = 1000 + day.Day
                };
            }
        }
    }

    private static decimal GetSeedMealCount(int day, string member) => member switch
    {
        "Rafi" => day <= 2 ? 3m : 2m,
        "Sadia" => day <= 5 ? 1m : 2m,
        "Tanvir" => day <= 11 ? 3m : 2m,
        "Nabil" => day <= 15 ? 2m : 1m,
        _ => 0m
    };

    private static List<Transfer> CalculateTransfers(IReadOnlyList<SettlementLine> lines)
    {
        var creditors = lines
            .Where(line => line.Net > 0m)
            .Select(line => new Balance(line.Member, line.Net))
            .OrderByDescending(line => line.Amount)
            .ToList();

        var debtors = lines
            .Where(line => line.Net < 0m)
            .Select(line => new Balance(line.Member, Math.Abs(line.Net)))
            .OrderByDescending(line => line.Amount)
            .ToList();

        var transfers = new List<Transfer>();
        var creditorIndex = 0;
        var debtorIndex = 0;

        while (creditorIndex < creditors.Count && debtorIndex < debtors.Count)
        {
            var creditor = creditors[creditorIndex];
            var debtor = debtors[debtorIndex];
            var amount = Math.Min(creditor.Amount, debtor.Amount);

            transfers.Add(new Transfer(transfers.Count + 1, debtor.Member, creditor.Member, amount));

            creditor.Amount -= amount;
            debtor.Amount -= amount;

            if (creditor.Amount == 0m)
            {
                creditorIndex++;
            }

            if (debtor.Amount == 0m)
            {
                debtorIndex++;
            }
        }

        return transfers;
    }

    private sealed class Balance(string member, decimal amount)
    {
        public string Member { get; } = member;
        public decimal Amount { get; set; } = amount;
    }

    public sealed class ExpenseEntry
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaidBy { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime DateUtc { get; set; }
    }

    public sealed class ContributionEntry
    {
        public string Member { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime PaidOnUtc { get; set; }
        public Guid? SourceExpenseId { get; set; }

        public static ContributionEntry FromExpense(ExpenseEntry expense) => new()
        {
            Member = expense.PaidBy,
            Amount = expense.Amount,
            Source = "Expense",
            Note = expense.Description,
            PaidOnUtc = expense.DateUtc,
            SourceExpenseId = expense.Id
        };
    }

    public sealed class MealCell
    {
        public decimal Value { get; set; }
        public long RowVersion { get; set; }
        public bool IsDirty { get; set; }
    }

    public readonly record struct MealCellKey(DateOnly Date, string Member);

    public sealed record SettlementResult(
        decimal MealSpending,
        decimal EqualSplitCosts,
        decimal TotalMeals,
        decimal PerMealRate,
        IReadOnlyList<SettlementLine> Lines,
        IReadOnlyList<Transfer> Transfers)
    {
        public decimal NetTotal => Lines.Sum(line => line.Net);
    }

    public sealed record SettlementLine(
        string Member,
        decimal Meals,
        decimal MealCost,
        decimal EqualShare,
        decimal Contributions,
        decimal Net,
        decimal RoundingAdjustment);

    public sealed record Transfer(int Number, string From, string To, decimal Amount);
}

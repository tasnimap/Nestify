namespace Nestify.Web.Services;

public sealed class SettlementWorkspaceService
{
    private static readonly string[] Members = ["Rafi", "Sadia", "Tanvir", "Nabil"];

    private readonly List<MonthKey> _months = [];
    private readonly Dictionary<MonthKey, MonthData> _data = [];
    private int _index;

    public SettlementWorkspaceService()
    {
        // Demo history. Closed months are finalized; the newest one is still open for edits.
        SeedMonth(new MonthKey(2026, 6), electricity: 3900m, water: 820m, finalized: true);
        SeedMonth(new MonthKey(2026, 7), electricity: 3600m, water: 880m, finalized: true);
        SeedMonth(new MonthKey(2026, 8), electricity: 3450m, water: 900m, finalized: true);
        SeedMonth(new MonthKey(2026, 9), electricity: 3240m, water: 860m, finalized: false);

        _index = _months.Count - 1;
    }

    public IReadOnlyList<string> MemberNames => Members;

    /// <summary>Which member the signed-in person is. Fixed while the workspace is still mock data.</summary>
    public string CurrentMember => Members[0];

    // ---------- month navigation ----------

    public IReadOnlyList<MonthKey> Months => _months;
    public MonthKey CurrentMonth => _months[_index];
    public string PeriodLabel => CurrentMonth.Label;

    public bool HasPreviousMonth => _index > 0;
    public bool HasNextMonth => _index < _months.Count - 1;

    /// <summary>True for the newest month, the only one still being filled in.</summary>
    public bool IsLatestMonth => _index == _months.Count - 1;

    public void GoToPreviousMonth()
    {
        if (HasPreviousMonth)
        {
            _index--;
        }
    }

    public void GoToNextMonth()
    {
        if (HasNextMonth)
        {
            _index++;
        }
    }

    private MonthData Current => _data[CurrentMonth];

    // ---------- current month ----------

    public IReadOnlyList<DateTime> Days => Current.Days;
    public IReadOnlyList<BillEntry> Bills => Current.Bills;
    public IReadOnlyList<PaymentEntry> Payments => Current.Payments;
    public bool IsFinalized => Current.IsFinalized;

    /// <summary>Everything the house owes this month, split equally between members.</summary>
    public decimal BillsTotal => Current.Bills.Sum(bill => bill.Amount);

    /// <summary>Money members handed over for groceries and bazar runs.</summary>
    public decimal MealFundTotal => Current.Payments
        .Where(payment => payment.Kind == PaymentKind.MealFund)
        .Sum(payment => payment.Amount);

    /// <summary>Money members handed over toward the shared bills.</summary>
    public decimal SharedFundTotal => Current.Payments
        .Where(payment => payment.Kind == PaymentKind.SharedBills)
        .Sum(payment => payment.Amount);

    public decimal TotalPaid => Current.Payments.Sum(payment => payment.Amount);

    /// <summary>Bill money the house has not collected from members yet.</summary>
    public decimal OutstandingBills => BillsTotal - SharedFundTotal;

    public decimal TotalMeals => Current.MealCells.Values.Sum(cell => cell.Total);

    public decimal PerMealRate => TotalMeals == 0m
        ? 0m
        : Math.Round(MealFundTotal / TotalMeals, 4, MidpointRounding.AwayFromZero);

    public DateTime CreateDateUtc(int day) =>
        new(CurrentMonth.Year, CurrentMonth.Month, day, 12, 0, 0, DateTimeKind.Utc);

    public static DateTime ToSettlementDateUtc(DateTime date) =>
        new(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Sensible default for the date pickers: today when we are in this month, else the 1st.</summary>
    public DateTime GetDefaultFormDate()
    {
        var today = DateTime.UtcNow;
        return today.Year == CurrentMonth.Year && today.Month == CurrentMonth.Month
            ? new DateTime(CurrentMonth.Year, CurrentMonth.Month, today.Day)
            : new DateTime(CurrentMonth.Year, CurrentMonth.Month, 1);
    }

    // ---------- bills ----------

    public BillEntry AddBill(string name, decimal amount, string icon = "other")
    {
        var entry = new BillEntry
        {
            Name = name.Trim(),
            Amount = amount,
            Icon = icon,
            IsCustom = true
        };

        Current.Bills.Add(entry);
        return entry;
    }

    public void UpdateBillAmount(Guid billId, decimal amount)
    {
        if (IsFinalized)
        {
            return;
        }

        var bill = Current.Bills.FirstOrDefault(item => item.Id == billId);
        if (bill is not null)
        {
            bill.Amount = Math.Max(0m, amount);
        }
    }

    public void ToggleBillSettled(Guid billId)
    {
        if (IsFinalized)
        {
            return;
        }

        var bill = Current.Bills.FirstOrDefault(item => item.Id == billId);
        if (bill is not null)
        {
            bill.IsSettled = !bill.IsSettled;
        }
    }

    public void RemoveBill(Guid billId)
    {
        if (IsFinalized)
        {
            return;
        }

        Current.Bills.RemoveAll(item => item.Id == billId && item.IsCustom);
    }

    // ---------- payments ----------

    public PaymentEntry AddPayment(string member, decimal amount, PaymentKind kind, string note, DateTime paidOn)
    {
        var entry = new PaymentEntry
        {
            Member = member,
            Amount = amount,
            Kind = kind,
            Note = note.Trim(),
            PaidOnUtc = ToSettlementDateUtc(paidOn)
        };

        Current.Payments.Insert(0, entry);
        return entry;
    }

    public void RemovePayment(Guid paymentId)
    {
        if (IsFinalized)
        {
            return;
        }

        Current.Payments.RemoveAll(item => item.Id == paymentId);
    }

    public decimal GetPaidTotal(string member) => Current.Payments
        .Where(item => item.Member == member)
        .Sum(item => item.Amount);

    public decimal GetPaidTotal(string member, PaymentKind kind) => Current.Payments
        .Where(item => item.Member == member && item.Kind == kind)
        .Sum(item => item.Amount);

    // ---------- meals ----------

    public MealCell GetMealCell(MealCellKey key) => Current.MealCells[key];

    public void ChangeMealSlot(MealCellKey key, MealSlot slot, decimal value)
    {
        if (IsFinalized)
        {
            return;
        }

        var cell = Current.MealCells[key];
        var clamped = Math.Clamp(value, 0m, 5m);

        switch (slot)
        {
            case MealSlot.Breakfast:
                cell.Breakfast = clamped;
                break;
            case MealSlot.Lunch:
                cell.Lunch = clamped;
                break;
            default:
                cell.Dinner = clamped;
                break;
        }

        cell.IsDirty = true;
    }

    public decimal GetMemberSlotTotal(string member, MealSlot slot) => Current.Days
        .Sum(day => GetMealCell(new MealCellKey(DateOnly.FromDateTime(day), member))[slot]);

    public bool HasUnsavedMeals => Current.MealCells.Values.Any(cell => cell.IsDirty);

    public decimal GetDayMealTotal(DateTime day) => Members
        .Sum(member => GetMealCell(new MealCellKey(DateOnly.FromDateTime(day), member)).Total);

    public decimal GetMemberMealTotal(string member) => Current.Days
        .Sum(day => GetMealCell(new MealCellKey(DateOnly.FromDateTime(day), member)).Total);

    public void MarkMealsSaved()
    {
        foreach (var cell in Current.MealCells.Values.Where(cell => cell.IsDirty))
        {
            cell.IsDirty = false;
        }
    }

    // ---------- settlement ----------

    public SettlementResult CalculateSettlement()
    {
        var billsTotal = BillsTotal;
        var mealFund = MealFundTotal;
        var rate = PerMealRate;

        var billShare = Members.Length == 0
            ? 0m
            : Math.Round(billsTotal / Members.Length, 2, MidpointRounding.AwayFromZero);

        var lines = Members
            .Select(member =>
            {
                var meals = GetMemberMealTotal(member);
                return new SettlementLine(
                    member,
                    meals,
                    Math.Round(meals * rate, 2, MidpointRounding.AwayFromZero),
                    billShare,
                    GetPaidTotal(member, PaymentKind.MealFund),
                    GetPaidTotal(member, PaymentKind.SharedBills),
                    0m,
                    0m);
            })
            .ToList();

        // Push the rounding crumbs onto the largest meal cost so the columns still add up.
        var residual = mealFund - lines.Sum(line => line.MealCost);
        if (residual != 0m && lines.Count > 0)
        {
            var index = lines
                .Select((line, i) => new { line, i })
                .OrderByDescending(item => item.line.MealCost)
                .ThenBy(item => item.line.Member, StringComparer.Ordinal)
                .First()
                .i;

            lines[index] = lines[index] with
            {
                MealCost = lines[index].MealCost + residual,
                RoundingAdjustment = residual
            };
        }

        lines = lines
            .Select(line => line with
            {
                Net = Math.Round(line.TotalPaid - line.MealCost - line.BillShare, 2, MidpointRounding.AwayFromZero)
            })
            .ToList();

        return new SettlementResult(billsTotal, mealFund, TotalMeals, rate, billShare, lines);
    }

    public void FinalizePeriod() => Current.IsFinalized = true;

    // ---------- seed data ----------

    private void SeedMonth(MonthKey key, decimal electricity, decimal water, bool finalized)
    {
        var days = Enumerable.Range(1, DateTime.DaysInMonth(key.Year, key.Month))
            .Select(day => new DateTime(key.Year, key.Month, day, 12, 0, 0, DateTimeKind.Utc))
            .ToArray();

        var month = new MonthData
        {
            Days = days,
            IsFinalized = finalized,
            Bills =
            [
                new BillEntry { Name = "House rent", Amount = 18000.00m, Icon = "rent", IsSettled = true },
                new BillEntry { Name = "Electricity bill", Amount = electricity, Icon = "electricity", IsSettled = finalized },
                new BillEntry { Name = "Water bill", Amount = water, Icon = "water", IsSettled = true },
                new BillEntry { Name = "Internet bill", Amount = 1150.00m, Icon = "internet", IsSettled = finalized },
                new BillEntry { Name = "Gas bill", Amount = 1080.00m, Icon = "gas", IsSettled = finalized },
                new BillEntry { Name = "Garbage collection fee", Amount = 300.00m, Icon = "garbage", IsSettled = true }
            ]
        };

        foreach (var day in days)
        {
            foreach (var member in Members)
            {
                var (breakfast, lunch, dinner) = GetSeedMeals(key.Month, day.Day, member);
                month.MealCells[new MealCellKey(DateOnly.FromDateTime(day), member)] = new MealCell
                {
                    Breakfast = breakfast,
                    Lunch = lunch,
                    Dinner = dinner
                };
            }
        }

        SeedPayments(key, month, finalized);

        _months.Add(key);
        _data[key] = month;
    }

    private void SeedPayments(MonthKey key, MonthData month, bool finalized)
    {
        void Pay(string member, decimal amount, PaymentKind kind, string note, int day) =>
            month.Payments.Insert(0, new PaymentEntry
            {
                Member = member,
                Amount = amount,
                Kind = kind,
                Note = note,
                PaidOnUtc = new DateTime(key.Year, key.Month, day, 12, 0, 0, DateTimeKind.Utc)
            });

        // Older months drift a little so browsing back actually shows different numbers.
        var drift = (9 - key.Month) * 200m;

        Pay("Rafi", 4500.00m + drift, PaymentKind.MealFund, "Groceries, first half", 10);
        Pay("Sadia", 2800.00m - (drift / 2), PaymentKind.MealFund, "Groceries, second half", 20);
        Pay("Nabil", 1900.00m + (drift / 4), PaymentKind.MealFund, "Rice and fish market", 27);

        if (finalized)
        {
            // A closed month collected every member's full share.
            var share = Math.Round(month.Bills.Sum(bill => bill.Amount) / Members.Length, 2, MidpointRounding.AwayFromZero);
            foreach (var member in Members)
            {
                Pay(member, share, PaymentKind.SharedBills, "Monthly share", 4);
            }

            return;
        }

        Pay("Rafi", 6200.00m, PaymentKind.SharedBills, "Rent share", 2);
        Pay("Sadia", 6175.00m, PaymentKind.SharedBills, "Rent share", 3);
        Pay("Tanvir", 5000.00m, PaymentKind.SharedBills, "Partial rent share", 5);
    }

    private static (decimal Breakfast, decimal Lunch, decimal Dinner) GetSeedMeals(int month, int day, string member)
    {
        var threshold = SeedThreshold(month, member);

        return member switch
        {
            "Rafi" => day <= threshold ? (1m, 1m, 1m) : (0.5m, 1m, 0.5m),
            "Sadia" => day <= threshold ? (0m, 1m, 0m) : (0.5m, 1m, 0.5m),
            "Tanvir" => day <= threshold ? (1m, 1m, 1m) : (0m, 1m, 1m),
            "Nabil" => day <= threshold ? (0m, 1m, 1m) : (0m, 1m, 0m),
            _ => (0m, 0m, 0m)
        };
    }

    /// <summary>Day the member's eating pattern changes, varied per month so the demo history differs.</summary>
    private static int SeedThreshold(int month, string member) => member switch
    {
        "Rafi" => month switch { 9 => 2, 8 => 6, 7 => 4, _ => 3 },
        "Sadia" => month switch { 9 => 5, 8 => 9, 7 => 7, _ => 12 },
        "Tanvir" => month switch { 9 => 11, 8 => 8, 7 => 14, _ => 10 },
        "Nabil" => month switch { 9 => 15, 8 => 20, 7 => 11, _ => 17 },
        _ => 0
    };

    private sealed class MonthData
    {
        public DateTime[] Days { get; init; } = [];
        public List<BillEntry> Bills { get; init; } = [];
        public List<PaymentEntry> Payments { get; } = [];
        public Dictionary<MealCellKey, MealCell> MealCells { get; } = [];
        public bool IsFinalized { get; set; }
    }

    public readonly record struct MonthKey(int Year, int Month)
    {
        public string Label => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public string ShortLabel => new DateTime(Year, Month, 1).ToString("MMM yyyy");
    }

    public enum PaymentKind
    {
        MealFund,
        SharedBills
    }

    public sealed class BillEntry
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Icon { get; set; } = "other";
        public bool IsSettled { get; set; }
        public bool IsCustom { get; init; }
    }

    public sealed class PaymentEntry
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Member { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentKind Kind { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime PaidOnUtc { get; set; }

        public string KindLabel => Kind == PaymentKind.MealFund ? "Meal fund" : "Shared bills";
    }

    public enum MealSlot
    {
        Breakfast,
        Lunch,
        Dinner
    }

    /// <summary>One member's meals for one day, split into breakfast + lunch + dinner.</summary>
    public sealed class MealCell
    {
        public decimal Breakfast { get; set; }
        public decimal Lunch { get; set; }
        public decimal Dinner { get; set; }
        public bool IsDirty { get; set; }

        public decimal Total => Breakfast + Lunch + Dinner;

        public decimal this[MealSlot slot] => slot switch
        {
            MealSlot.Breakfast => Breakfast,
            MealSlot.Lunch => Lunch,
            _ => Dinner
        };
    }

    public readonly record struct MealCellKey(DateOnly Date, string Member);

    public sealed record SettlementResult(
        decimal BillsTotal,
        decimal MealFund,
        decimal TotalMeals,
        decimal PerMealRate,
        decimal BillShare,
        IReadOnlyList<SettlementLine> Lines)
    {
        public decimal NetTotal => Lines.Sum(line => line.Net);
    }

    public sealed record SettlementLine(
        string Member,
        decimal Meals,
        decimal MealCost,
        decimal BillShare,
        decimal MealFundPaid,
        decimal SharedPaid,
        decimal Net,
        decimal RoundingAdjustment)
    {
        public decimal TotalPaid => MealFundPaid + SharedPaid;
        public decimal TotalOwed => MealCost + BillShare;
        public bool HouseOwesMember => Net > 0m;
    }
}

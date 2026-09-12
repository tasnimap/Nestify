namespace Nestify.Shared.Dtos.Settlement;

public sealed class SettlementWorkspaceDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsFinalized { get; set; }
    public decimal BillsTotal { get; set; }
    public decimal MealFundTotal { get; set; }
    public decimal SharedFundTotal { get; set; }
    public decimal TotalMeals { get; set; }
    public decimal PerMealRate { get; set; }
    public decimal BillShare { get; set; }
    public decimal OutstandingBills { get; set; }
    public List<SettlementMemberDto> Members { get; set; } = [];
    public List<SettlementBillDto> Bills { get; set; } = [];
    public List<SettlementPaymentDto> Payments { get; set; } = [];
    public List<SettlementMealDto> Meals { get; set; } = [];
    public SettlementResultDto? Result { get; set; }
}

public sealed class SettlementMemberDto
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public short Role { get; set; }
    public bool IsMe { get; set; }
}

public sealed class SettlementBillDto
{
    public long Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime SpentOn { get; set; }
    public long SpentByUserId { get; set; }
    public string SpentByName { get; set; } = string.Empty;
}

public sealed class SettlementPaymentDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public short FundType { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime PaidOn { get; set; }
}

public sealed class SettlementMealDto
{
    public long UserId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public DateTime MealDate { get; set; }
    public decimal MealCount { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

public sealed class SettlementResultDto
{
    public decimal BillsTotal { get; set; }
    public decimal MealFund { get; set; }
    public decimal TotalMeals { get; set; }
    public decimal PerMealRate { get; set; }
    public decimal BillShare { get; set; }
    public decimal NetTotal { get; set; }
    public List<SettlementLineDto> Lines { get; set; } = [];
    public List<SettlementTransferDto> Transfers { get; set; } = [];
}

public sealed class SettlementLineDto
{
    public long UserId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public decimal Meals { get; set; }
    public decimal MealCost { get; set; }
    public decimal EqualShare { get; set; }
    public decimal Contributions { get; set; }
    public decimal RoundingAdjustment { get; set; }
    public decimal NetAmount { get; set; }
}

public sealed class SettlementTransferDto
{
    public long FromUserId { get; set; }
    public string FromMemberName { get; set; } = string.Empty;
    public long ToUserId { get; set; }
    public string ToMemberName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class CreateSettlementBillRequest
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime SpentOn { get; set; }
}

public sealed class CreateSettlementPaymentRequest
{
    public long UserId { get; set; }
    public decimal Amount { get; set; }
    public short FundType { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime PaidOn { get; set; }
}

public sealed class SaveSettlementMealsRequest
{
    public List<SettlementMealChangeDto> Entries { get; set; } = [];
}

public sealed class SettlementMealChangeDto
{
    public long UserId { get; set; }
    public DateTime MealDate { get; set; }
    public decimal MealCount { get; set; }
}

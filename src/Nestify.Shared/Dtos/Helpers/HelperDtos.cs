namespace Nestify.Shared.Dtos.Helpers;

public enum ServiceType
{
    Cooking,
    Cleaning,
    Babysitting,
    ElderCare,
    Laundry,
    General
}

public enum DistanceBand
{
    Within1Km,
    Within2Km,
    Within5Km,
    Over5Km
}

public enum EngagementStatus
{
    Requested,
    Declined,
    HelperConfirmed,
    Active,
    Completed
}

public enum EngagementRole
{
    Client,
    Helper
}

public enum HelperSortOption
{
    RatingDesc,
    RateAsc,
    RateDesc,
    DistanceAsc
}

public sealed class HelperSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ServiceType> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public DistanceBand? Distance { get; set; }
}

public sealed class HelperDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ServiceType> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public string AvailabilityWindow { get; set; } = string.Empty;
    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public DistanceBand? Distance { get; set; }
    public bool IsMine { get; set; }
}

public sealed class HelperRegistrationDto
{
    public List<ServiceType> Services { get; set; } = new();
    public string AvailabilityWindow { get; set; } = string.Empty;
    public decimal MonthlyRate { get; set; }
    public int? DivisionId { get; set; }
    public int? DistrictId { get; set; }
    public int? UpazilaId { get; set; }
}

public sealed class HelperFilterDto
{
    public int? DivisionId { get; set; }
    public int? DistrictId { get; set; }
    public int? UpazilaId { get; set; }
    public ServiceType? ServiceType { get; set; }
    public decimal? MaxMonthlyRate { get; set; }
    public double? MinRating { get; set; }
    public HelperSortOption Sort { get; set; } = HelperSortOption.RatingDesc;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}

public sealed class HelperPageDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed class ReviewDto
{
    public string ReviewerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class EngagementDto
{
    public string Id { get; set; } = string.Empty;
    public string HelperId { get; set; } = string.Empty;
    public string HelperName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public EngagementRole MyRole { get; set; }
    public EngagementStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool ClientMarkedComplete { get; set; }
    public bool HelperMarkedComplete { get; set; }
    public bool CanReview { get; set; }
}
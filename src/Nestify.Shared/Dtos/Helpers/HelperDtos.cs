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
    public bool IsVerified { get; set; }
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
    public bool IsVerified { get; set; }
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

public sealed class HelperVerificationDto
{
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
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

/// <summary>One hour a client picked off the helper's availability board.</summary>
public sealed class EngagementSlotDto
{
    public DateTime Date { get; set; }

    /// <summary>Start of the hour, 6 to 23.</summary>
    public int Hour { get; set; }
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

public sealed class HelperVerificationStatusDto
{
    public bool IsPending { get; set; }
    public string? DocumentType { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
}

public sealed class HelperWorkspaceDashboardDto
{
    public int PendingRequestCount { get; set; }
    public int ActiveJobCount { get; set; }
    public decimal MonthlyEarnings { get; set; }
    public int HoursThisWeek { get; set; }
    public int VisitsThisWeek { get; set; }
    public double RatingAverage { get; set; }
    public int ReviewCount { get; set; }
    public List<HelperWorkspaceVisitDto> TodayVisits { get; set; } = new();
    public List<HelperWorkspaceRequestDto> NewRequests { get; set; } = new();
    public List<HelperAvailabilityDayDto> OpenHours { get; set; } = new();
}

public sealed class HelperAvailabilityDto
{
    public bool IsAvailable { get; set; }
    public List<HelperAvailabilitySlotDto> Slots { get; set; } = new();
}

public sealed class HelperAvailabilitySlotDto
{
    public int DayOfWeek { get; set; }
    public int Hour { get; set; }
    public bool IsOpen { get; set; }
    public bool IsBooked { get; set; }
}

public sealed class HelperAvailabilityDayDto
{
    public int DayOfWeek { get; set; }
    public int OpenHours { get; set; }
    public int BookedHours { get; set; }
}

public sealed class HelperWorkspaceScheduleDto
{
    public DateTime WeekStart { get; set; }
    public List<HelperWorkspaceVisitDto> Visits { get; set; } = new();
    public HelperWorkspaceVisitDto? NextVisit { get; set; }
}

public sealed class HelperWorkspaceVisitDto
{
    public string Id { get; set; } = string.Empty;
    public string EngagementId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int StartHour { get; set; }
    public int EndHour { get; set; }
    public bool IsDone { get; set; }
}

public sealed class HelperWorkspaceEngagementsDto
{
    public List<HelperWorkspaceRequestDto> Requests { get; set; } = new();
    public List<HelperWorkspaceEngagementDto> Current { get; set; } = new();
    public List<HelperWorkspaceEngagementDto> Past { get; set; } = new();
}

public sealed class HelperWorkspaceRequestDto
{
    public string Id { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public bool ClientVerified { get; set; }
    public string Area { get; set; } = string.Empty;
    public string HomeType { get; set; } = string.Empty;
    public List<string> Services { get; set; } = new();
    public decimal OfferedRate { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public List<EngagementSlotDto> Slots { get; set; } = new();
    public string? DeclineReason { get; set; }
}

public sealed class HelperWorkspaceEngagementDto
{
    public string Id { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public List<string> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public DateTime StartedOn { get; set; }
    public int Status { get; set; }
    public int WeeklyHours { get; set; }
    public int VisitsDone { get; set; }
    public int VisitsPlanned { get; set; }
    public bool HelperMarkedComplete { get; set; }
    public bool ClientMarkedComplete { get; set; }
}

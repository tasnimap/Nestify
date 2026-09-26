namespace Nestify.Shared.Dtos.Helpers;

// Numbered the same way as helper_services.service_type in Domestic_Help.sql.
public enum ServiceType
{
    Cooking = 1,
    Cleaning = 2,
    Laundry = 3,
    Dishwashing = 4,
    GroceryRuns = 5,
    General = 6
}

public static class ServiceTypes
{
    public static string Label(ServiceType type) => type switch
    {
        ServiceType.GroceryRuns => "Grocery runs",
        ServiceType.General => "General help",
        _ => type.ToString()
    };
}

// Mirrors service_engagements.status.
public enum EngagementStatus
{
    Requested = 1,
    Active = 2,
    Completed = 3,
    Declined = 4,
    Cancelled = 5
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
    ExperienceDesc
}

public enum HelperVerificationState
{
    NotApplied,
    Pending,
    Verified,
    Rejected
}

public sealed class HelperSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public List<ServiceType> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public int ExperienceYears { get; set; }
    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
}

/// <summary>The public profile a bachelor sees before booking.</summary>
public sealed class HelperDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Languages { get; set; } = string.Empty;
    public List<ServiceType> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public int ExperienceYears { get; set; }
    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool IsAcceptingBookings { get; set; }
    public DateTime MemberSinceUtc { get; set; }
    public List<HelperAvailabilitySlotDto> Availability { get; set; } = new();
    public bool IsMine { get; set; }
    public string? ActiveEngagementId { get; set; }
    public bool IsWorkingForMyHome { get; set; }
    public bool CanManageEngagement { get; set; }
}

/// <summary>One hour on the weekly board. DayOfWeek is 0 Sunday .. 6 Saturday.</summary>
public sealed class HelperAvailabilitySlotDto
{
    public int DayOfWeek { get; set; }
    public int Hour { get; set; }
    public bool IsOpen { get; set; }
    public bool IsBooked { get; set; }
}

public sealed class HelperAvailabilityDto
{
    public bool IsAvailable { get; set; }
    public List<HelperAvailabilitySlotDto> Slots { get; set; } = new();
}

/// <summary>
/// The helper's own profile: her users row plus everything in Domestic_Help.sql.
/// Sign-up only asks for name, email, phone and password; the rest is filled
/// in from the profile page, and IsComplete says whether that has been done.
/// </summary>
public sealed class HelperProfileDto
{
    public string Id { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime JoinedAtUtc { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Languages { get; set; } = string.Empty;
    public List<ServiceType> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public int ExperienceYears { get; set; }
    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public bool IsVerified { get; set; }
    public bool IsAcceptingBookings { get; set; }
    public int? DivisionId { get; set; }
    public int? DistrictId { get; set; }
    public int? UpazilaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int CompletedCount { get; set; }
    public int ActiveCount { get; set; }
    public int OpenHoursPerWeek { get; set; }
    public HelperVerificationStatusDto Verification { get; set; } = new();
}

/// <summary>What a helper fills in when registering or editing her profile.</summary>
public sealed class HelperProfileFormDto
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Languages { get; set; } = "Bangla";
    public List<ServiceType> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public int ExperienceYears { get; set; }
    public int? DivisionId { get; set; }
    public int? DistrictId { get; set; }
    public int? UpazilaId { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    // Dhaka by default so the map has somewhere to open.
    public double Latitude { get; set; } = 23.7806;
    public double Longitude { get; set; } = 90.4074;
}

public sealed class HelperVerificationStatusDto
{
    public HelperVerificationState State { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>What the helper nav needs: her name, photo and the request badge.</summary>
public sealed class HelperNavDto
{
    public bool IsProfileComplete { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public int PendingRequestCount { get; set; }
}

public sealed class HelperFilterDto
{
    public int? DivisionId { get; set; }
    public int? DistrictId { get; set; }
    public int? UpazilaId { get; set; }
    public ServiceType? ServiceType { get; set; }
    public decimal? MaxMonthlyRate { get; set; }
    public double? MinRating { get; set; }
    public bool VerifiedOnly { get; set; }
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
    public string Id { get; set; } = string.Empty;
    public string ReviewerName { get; set; } = string.Empty;
    public string ReviewerPhotoUrl { get; set; } = string.Empty;
    public string HomeName { get; set; } = string.Empty;
    public List<string> Services { get; set; } = new();
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? Reply { get; set; }
    public DateTime? RepliedAtUtc { get; set; }
}

/// <summary>Everything on the helper's reviews page.</summary>
public sealed class HelperReviewsDto
{
    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public List<ReviewDto> Reviews { get; set; } = new();
}

/// <summary>One weekly hour a client picked off the helper's board.</summary>
public sealed class EngagementSlotDto
{
    public int DayOfWeek { get; set; }
    public int Hour { get; set; }
}

/// <summary>What a bachelor sends when asking a helper for an engagement.</summary>
public sealed class EngagementRequestDto
{
    public List<ServiceType> Services { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public List<EngagementSlotDto> Slots { get; set; } = new();
}

/// <summary>
/// An engagement as a bachelor sees it: either one they asked for on behalf
/// of their home, or a helper who worked at their home while they lived there.
/// </summary>
public sealed class EngagementDto
{
    public string Id { get; set; } = string.Empty;
    public string HelperId { get; set; } = string.Empty;
    public string HelperName { get; set; } = string.Empty;
    public string HelperPhotoUrl { get; set; } = string.Empty;
    public string? HelperPhone { get; set; }          // shared once the helper accepts
    public string HelperArea { get; set; } = string.Empty;
    public string HomeName { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public bool IsRequester { get; set; }
    public DateTime? JoinedOn { get; set; }           // when she started at the home
    public DateTime? LeftOn { get; set; }             // when the engagement ended
    public bool IsCurrent { get; set; }               // active engagement with an open placement
    public List<ServiceType> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<EngagementSlotDto> Slots { get; set; } = new();
    public EngagementStatus Status { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? StartDate { get; set; }
    public string? DeclineReason { get; set; }
    public bool ClientMarkedComplete { get; set; }
    public bool HelperMarkedComplete { get; set; }
    public bool CanManage { get; set; }               // current manager / co-manager of the home
    public bool HasReview { get; set; }               // this user already reviewed her for this placement
    public bool CanReview { get; set; }               // lived in the home while she worked there, and has not reviewed yet
}

public sealed class SubmitReviewDto
{
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public sealed class ReviewReplyDto
{
    public string Reply { get; set; } = string.Empty;
}

public sealed class DeclineEngagementDto
{
    public string? Reason { get; set; }
}

// ------------------------------------------------------------ helper workspace

public sealed class HelperWorkspaceDashboardDto
{
    public int PendingRequestCount { get; set; }
    public int ActiveJobCount { get; set; }
    public decimal MonthlyEarnings { get; set; }
    public int HoursThisWeek { get; set; }
    public int VisitsThisWeek { get; set; }
    public double RatingAverage { get; set; }
    public int ReviewCount { get; set; }
    public bool IsVerified { get; set; }
    public bool IsProfileComplete { get; set; }
    public List<HelperWorkspaceVisitDto> TodayVisits { get; set; } = new();
    public List<HelperWorkspaceRequestDto> NewRequests { get; set; } = new();
}

public sealed class HelperWorkspaceScheduleDto
{
    public DateTime WeekStart { get; set; }
    public List<HelperWorkspaceVisitDto> Visits { get; set; } = new();
    public HelperWorkspaceVisitDto? NextVisit { get; set; }
}

/// <summary>One hour of one engagement on one date, worked out from the weekly slots.</summary>
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
    public string ClientPhotoUrl { get; set; } = string.Empty;
    public bool ClientVerified { get; set; }
    public string HomeName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public List<string> Services { get; set; } = new();
    public decimal OfferedRate { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public List<EngagementSlotDto> Slots { get; set; } = new();
}

public sealed class HelperWorkspaceEngagementDto
{
    public string Id { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhotoUrl { get; set; } = string.Empty;
    public string HomeName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public List<string> Services { get; set; } = new();
    public decimal MonthlyRate { get; set; }
    public DateTime StartedOn { get; set; }
    public EngagementStatus Status { get; set; }
    public int WeeklyHours { get; set; }
    public int VisitsDone { get; set; }
    public int VisitsPlanned { get; set; }
    public bool HelperMarkedComplete { get; set; }
    public bool ClientMarkedComplete { get; set; }
    public List<EngagementSlotDto> Slots { get; set; } = new();
}

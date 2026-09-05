namespace Nestify.Shared.Dtos.Housing;

public enum ListingType
{
    SingleSeat,
    MultipleSeats,
    EntireHouse
}

public enum PostStatus
{
    Active,
    Closed
}

public enum BookingStatus
{
    Pending,
    Accepted,
    Rejected,
    Withdrawn
}

public enum Gender
{
    Male,
    Female
}

public enum Occupation
{
    Student,
    Working,
    Both
}

public enum MaritalStatus
{
    Single,
    Married
}

/// <summary>
/// Eligibility requirements attached to a post (§2.5). Every field is nullable —
/// null means "no constraint". Rendered as chips; never used to filter client-side (§5.3).
/// </summary>
public sealed class EligibilityDto
{
    public Gender? Gender { get; set; }
    public Occupation? Occupation { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public bool VerifiedOnly { get; set; }
    public bool StudentOnly { get; set; }

    public bool IsEmpty =>
        Gender is null && Occupation is null && MaritalStatus is null &&
        MinAge is null && MaxAge is null && !VerifiedOnly && !StudentOnly;
}

/// <summary>Card-sized projection used by the Browse grid and "my posts".</summary>
public sealed class HousingPostSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ListingType ListingType { get; set; }
    public int SeatsAvailable { get; set; }
    public decimal MonthlyRent { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public PostStatus Status { get; set; } = PostStatus.Active;
    public DateTime CreatedAtUtc { get; set; }
    public List<string> ImageUrls { get; set; } = new();
}

/// <summary>Full post view for <c>/housing/{id}</c>.</summary>
public sealed class HousingPostDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ListingType ListingType { get; set; }
    public int SeatsAvailable { get; set; }
    public decimal MonthlyRent { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public PostStatus Status { get; set; } = PostStatus.Active;
    public DateTime CreatedAtUtc { get; set; }
    public EligibilityDto Eligibility { get; set; } = new();

    /// <summary>True when the signed-in user owns this post — gates Edit/Close, hides Book.</summary>
    public bool IsMine { get; set; }
    public List<string> ImageUrls { get; set; } = new();
}

/// <summary>Query parameters for the Browse grid (§5.4). Area filtering is a convenience, not a security boundary.</summary>
public sealed class HousingPostFilterDto
{
    public int? DivisionId { get; set; }
    public int? DistrictId { get; set; }
    public int? UpazilaId { get; set; }
    public ListingType? ListingType { get; set; }
    public decimal? MaxRent { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}

/// <summary>A page of results plus the counters the grid header needs.</summary>
public sealed class HousingPageDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>Payload for <c>/housing/new</c>.</summary>
public sealed class CreateHousingPostRequestDto
{
    public string HouseId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ListingType ListingType { get; set; }
    public int SeatsAvailable { get; set; }
    public decimal MonthlyRent { get; set; }
    public EligibilityDto Eligibility { get; set; } = new();
}

/// <summary>Payload for <c>/housing/{id}/edit</c>. No HouseId — a post cannot be reparented (§3.6).</summary>
public sealed class UpdateHousingPostRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ListingType ListingType { get; set; }
    public int SeatsAvailable { get; set; }
    public decimal MonthlyRent { get; set; }
    public EligibilityDto Eligibility { get; set; } = new();
}

/// <summary>
/// TEMPORARY bridge for the house selector on <c>/housing/new</c>, until Obonti's
/// <c>houses/mine</c> (M3) lands on main. Backed only by <c>MockHouseLookupService</c> —
/// once the real IHouseService/HouseSummaryDto ships, delete this DTO, IHouseLookupService,
/// and MockHouseLookupService, and point the selector at the real thing instead.
/// </summary>
public sealed class HouseOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
}

/// <summary>
/// Row on <c>/housing/mine</c> (§3.6 <c>housing-posts/mine</c>, deliberately bypasses
/// <c>VisibleTo</c> so an owner sees their own post regardless of its requirements).
/// The booking counts are wired up once bookings-core lands — until then they read 0.
/// </summary>
public sealed class MyHousingPostDto
{
    public HousingPostSummaryDto Post { get; set; } = new();
    public int BookingRequestCount { get; set; }
    public int PendingBookingRequestCount { get; set; }
}

/// <summary>
/// Booking request info shown to a post manager (§11.4.4). Never carries contact —
/// see GetBookingContactAsync for PII disclosure (§11.4.2).
/// </summary>
public sealed class BookingRequesterDto
{
    public string BookingId { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? Message { get; set; }
}

/// <summary>
/// Payload for rejecting a booking request.
/// </summary>
public sealed class RejectBookingRequestDto
{
    public string? Message { get; set; }
}

/// <summary>
/// Contact disclosure for an Accepted booking (§11.4.2). Only returns when the
/// booking is Accepted and the caller is a party to it (requester or post manager).
/// </summary>
public sealed class ContactDisclosureDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

/// <summary>
/// Seeker's view of their own booking request (§11.4 for seeker-side flow).
/// </summary>
public sealed class MyBookingDto
{
    public string BookingId { get; set; } = string.Empty;
    public HousingPostSummaryDto Post { get; set; } = new();
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime RequestedAtUtc { get; set; }
    public string? Message { get; set; }
    public string? ManagerName { get; set; } // Populated only when Status == Accepted
}
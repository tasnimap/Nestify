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
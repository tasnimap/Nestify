// src/Nestify.Shared/Dtos/Marketplace/MarketplaceDtos.cs
// M4 — Second-hand marketplace. DTOs + enums only (no EF types).
// One file per module, per the frontend-phase distribution rules.
namespace Nestify.Shared.Dtos.Marketplace;

/// <summary>Item categories. Fixed set — the browse filter binds to these, never a free string.</summary>
public enum MarketplaceCategory
{
    Furniture,
    Electronics,
    Appliances,
    Kitchen,
    Books,
    Bedding,
    Other
}

/// <summary>Condition grades shown as chips on the card and detail page.</summary>
public enum ItemCondition
{
    New,
    LikeNew,
    Good,
    Fair
}

/// <summary>
/// Sort options for the browse grid. Comes from this fixed enum, never a column
/// name in a query string (§11.5.1).
/// </summary>
public enum MarketplaceSort
{
    Newest,
    PriceLowToHigh,
    PriceHighToLow
}

/// <summary>Lifecycle of a listing. Mirrors M1's post Active/Closed pair (§8.1).</summary>
public enum ListingStatus
{
    Active,
    Sold,
    Removed
}

/// <summary>
/// Buy-interest state machine. Mirrors M1's booking Pending/Accepted/Rejected/Withdrawn.
/// Pending → Accepted is the contact-disclosure transition.
/// </summary>
public enum BuyInterestStatus
{
    Pending,
    Accepted,
    Declined,
    Withdrawn
}

/// <summary>Card-sized projection used by the browse grid and "my listings".</summary>
public sealed class MarketplaceItemSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal PriceBdt { get; set; }
    public MarketplaceCategory Category { get; set; }
    public ItemCondition Condition { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string SellerDisplayName { get; set; } = string.Empty;
    public bool SellerVerified { get; set; }
    public DateTime PostedAtUtc { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Active;

    /// <summary>First image. An http(s) URL renders as a photo; anything else renders as a generated tile.</summary>
    public string CoverImage { get; set; } = string.Empty;
}

/// <summary>Full item view for <c>/marketplace/items/{id}</c>.</summary>
public sealed class MarketplaceItemDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceBdt { get; set; }
    public MarketplaceCategory Category { get; set; }
    public ItemCondition Condition { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public DateTime PostedAtUtc { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Active;

    public IReadOnlyList<string> Images { get; set; } = new List<string>();

    public string SellerId { get; set; } = string.Empty;
    public string SellerDisplayName { get; set; } = string.Empty;
    public bool SellerVerified { get; set; }
    public DateTime SellerJoinedUtc { get; set; }

    /// <summary>True when the signed-in user owns this listing — the Buy button is absent in that case.</summary>
    public bool IsMine { get; set; }

    /// <summary>The viewer already has an open buy interest on this item.</summary>
    public bool HasActiveInterest { get; set; }
}

/// <summary>Query parameters for the browse grid.</summary>
public sealed class MarketplaceItemFilterDto
{
    public string? Search { get; set; }
    public MarketplaceCategory? Category { get; set; }
    public ItemCondition? Condition { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Division { get; set; }
    public MarketplaceSort Sort { get; set; } = MarketplaceSort.Newest;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

/// <summary>A page of results plus the counters the grid header needs.</summary>
public sealed class MarketplacePageDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>Payload for <c>/marketplace/sell</c>.</summary>
public sealed class CreateMarketplaceItemDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MarketplaceCategory Category { get; set; } = MarketplaceCategory.Furniture;
    public ItemCondition Condition { get; set; } = ItemCondition.Good;
    public decimal PriceBdt { get; set; }
    public string Division { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public IReadOnlyList<string> Images { get; set; } = new List<string>();
}

/// <summary>Payload for <c>/marketplace/items/{id}/edit</c>. No seller field, no status field.</summary>
public sealed class UpdateMarketplaceItemDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MarketplaceCategory Category { get; set; }
    public ItemCondition Condition { get; set; }
    public decimal PriceBdt { get; set; }
    public string Division { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public IReadOnlyList<string> Images { get; set; } = new List<string>();
}

/// <summary>Row on the seller's "my listings" page.</summary>
public sealed class MyListingDto
{
    public MarketplaceItemSummaryDto Item { get; set; } = new();
    public int InterestCount { get; set; }
    public int PendingInterestCount { get; set; }
    public int ViewCount { get; set; }
}

/// <summary>
/// Disclosed contact block. Only ever populated on an <see cref="BuyInterestStatus.Accepted"/>
/// interest — before that the property is null and no contact markup renders (§11.4).
/// </summary>
public sealed class MarketplaceContactDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? PreferredHandoverArea { get; set; }
}

/// <summary>Seller-facing row on <c>/marketplace/items/{id}/interests</c>.</summary>
public sealed class BuyInterestDto
{
    public string Id { get; set; } = string.Empty;
    public string BuyerId { get; set; } = string.Empty;
    public string BuyerDisplayName { get; set; } = string.Empty;
    public bool BuyerVerified { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public BuyInterestStatus Status { get; set; }

    /// <summary>Non-null only when <see cref="Status"/> is Accepted.</summary>
    public MarketplaceContactDto? Contact { get; set; }
}

/// <summary>Buyer-facing row on <c>/buy-interests/mine</c>.</summary>
public sealed class MyBuyInterestDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public decimal ItemPriceBdt { get; set; }
    public string ItemCoverImage { get; set; } = string.Empty;
    public string SellerDisplayName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public BuyInterestStatus Status { get; set; }

    /// <summary>Non-null only when <see cref="Status"/> is Accepted.</summary>
    public MarketplaceContactDto? Contact { get; set; }
}

/// <summary>
/// Advisory price band for the sell form's ML suggestion slot (§10.8). The model
/// advises; the seller decides. Backend-generated in a later phase.
/// </summary>
public sealed class PriceSuggestionDto
{
    public decimal SuggestedLow { get; set; }
    public decimal SuggestedHigh { get; set; }
    public decimal SuggestedPoint { get; set; }
    public string Basis { get; set; } = string.Empty;
    public int ComparableCount { get; set; }
}

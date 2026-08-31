// src/Nestify.Web/Services/Interfaces/IMarketplaceService.cs
using Nestify.Shared.Dtos.Marketplace;

namespace Nestify.Web.Services.Interfaces;

/// <summary>
/// The contract every M4 page depends on. Mirrors the endpoints in §3.11 of the
/// implementation plan — same operations, same DTOs, same return shapes — so the
/// mock and the real HttpClient implementation are swappable with one line in
/// <c>Program.cs</c>.
/// </summary>
public interface IMarketplaceService
{
    // ---- Browse + detail -------------------------------------------------
    Task<MarketplacePageDto<MarketplaceItemSummaryDto>> BrowseAsync(MarketplaceItemFilterDto filter);
    Task<MarketplaceItemDetailDto?> GetItemAsync(string id);

    // ---- Create + edit + mine -----------------------------------------
    Task<string> CreateItemAsync(CreateMarketplaceItemDto dto);
    Task<MarketplaceItemDetailDto?> GetItemForEditAsync(string id);
    Task<bool> UpdateItemAsync(string id, UpdateMarketplaceItemDto dto);
    Task<IReadOnlyList<MyListingDto>> GetMyListingsAsync();
    Task<bool> MarkSoldAsync(string id);
    Task<bool> DeleteItemAsync(string id);

    /// <summary>Advisory only — fills the sell form's ML price-suggestion slot (§10.8).</summary>
    Task<PriceSuggestionDto?> GetPriceSuggestionAsync(MarketplaceCategory category, ItemCondition condition);

    // ---- Two-party buy-interest flow --------------------------------------
    Task<bool> ExpressInterestAsync(string itemId, string message);
    Task<IReadOnlyList<BuyInterestDto>> GetItemInterestsAsync(string itemId);
    Task<bool> RespondToInterestAsync(string interestId, bool accept);
    Task<IReadOnlyList<MyBuyInterestDto>> GetMyBuyInterestsAsync();
    Task<bool> WithdrawInterestAsync(string interestId);
}

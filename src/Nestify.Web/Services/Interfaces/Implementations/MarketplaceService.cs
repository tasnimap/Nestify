// src/Nestify.Web/Services/Implementations/MarketplaceService.cs
// NEXT-PHASE STUB. When the §3.11 endpoints land, this HttpClient implementation
// replaces MockMarketplaceService with a one-line swap in Program.cs and no page
// changes. Left unimplemented on purpose during the frontend phase.
using Nestify.Shared.Dtos.Marketplace;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MarketplaceService : IMarketplaceService
{
    private readonly HttpClient _httpClient;

    public MarketplaceService(HttpClient httpClient) => _httpClient = httpClient;

    private static NotImplementedException NotWiredYet([System.Runtime.CompilerServices.CallerMemberName] string op = "")
        => new($"IMarketplaceService.{op} is wired to the real API in the backend phase. " +
               "Register MockMarketplaceService in Program.cs for the frontend phase.");

    public Task<MarketplacePageDto<MarketplaceItemSummaryDto>> BrowseAsync(MarketplaceItemFilterDto filter) => throw NotWiredYet();
    public Task<MarketplaceItemDetailDto?> GetItemAsync(string id) => throw NotWiredYet();
    public Task<string> CreateItemAsync(CreateMarketplaceItemDto dto) => throw NotWiredYet();
    public Task<MarketplaceItemDetailDto?> GetItemForEditAsync(string id) => throw NotWiredYet();
    public Task<bool> UpdateItemAsync(string id, UpdateMarketplaceItemDto dto) => throw NotWiredYet();
    public Task<IReadOnlyList<MyListingDto>> GetMyListingsAsync() => throw NotWiredYet();
    public Task<bool> MarkSoldAsync(string id) => throw NotWiredYet();
    public Task<bool> DeleteItemAsync(string id) => throw NotWiredYet();
    public Task<PriceSuggestionDto?> GetPriceSuggestionAsync(MarketplaceCategory category, ItemCondition condition) => throw NotWiredYet();
    public Task<bool> ExpressInterestAsync(string itemId, string message) => throw NotWiredYet();
    public Task<IReadOnlyList<BuyInterestDto>> GetItemInterestsAsync(string itemId) => throw NotWiredYet();
    public Task<bool> RespondToInterestAsync(string interestId, bool accept) => throw NotWiredYet();
    public Task<IReadOnlyList<MyBuyInterestDto>> GetMyBuyInterestsAsync() => throw NotWiredYet();
    public Task<bool> WithdrawInterestAsync(string interestId) => throw NotWiredYet();
}

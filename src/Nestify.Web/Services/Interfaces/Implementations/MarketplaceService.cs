// src/Nestify.Web/Services/Implementations/MarketplaceService.cs
// The real IMarketplaceService, talking to api/v1/marketplace (Marketplace.sql).
// The pages only see the DTOs, so nothing in them changes.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Nestify.Shared.Dtos.Marketplace;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class MarketplaceService : IMarketplaceService
{
    private readonly HttpClient _httpClient;

    public MarketplaceService(HttpClient httpClient) => _httpClient = httpClient;

    // ---- Browse + detail ----

    public async Task<MarketplacePageDto<MarketplaceItemSummaryDto>> BrowseAsync(MarketplaceItemFilterDto filter)
    {
        var query = new List<string>
        {
            $"sort={filter.Sort}",
            $"page={filter.Page}",
            $"pageSize={filter.PageSize}"
        };

        if (!string.IsNullOrWhiteSpace(filter.Search)) query.Add("search=" + Uri.EscapeDataString(filter.Search));
        if (filter.Category is { } category) query.Add($"category={category}");
        if (filter.Condition is { } condition) query.Add($"condition={condition}");
        if (filter.MinPrice is { } min) query.Add("minPrice=" + min.ToString(CultureInfo.InvariantCulture));
        if (filter.MaxPrice is { } max) query.Add("maxPrice=" + max.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(filter.Division)) query.Add("division=" + Uri.EscapeDataString(filter.Division));
        if (!string.IsNullOrWhiteSpace(filter.District)) query.Add("district=" + Uri.EscapeDataString(filter.District));
        if (!string.IsNullOrWhiteSpace(filter.Upazila)) query.Add("upazila=" + Uri.EscapeDataString(filter.Upazila));

        var page = await _httpClient.GetFromJsonAsync<MarketplacePageDto<MarketplaceItemSummaryDto>>(
            "api/v1/marketplace/items?" + string.Join("&", query));
        return page ?? new MarketplacePageDto<MarketplaceItemSummaryDto> { Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<MarketplaceItemDetailDto?> GetItemAsync(string id) =>
        GetOrNullAsync<MarketplaceItemDetailDto>($"api/v1/marketplace/items/{id}");

    // ---- Create + edit + mine ----

    public async Task<string> CreateItemAsync(CreateMarketplaceItemDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/marketplace/items", dto);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException(await ReadMessageAsync(response) ?? "Could not publish the listing.");
        }

        var body = await response.Content.ReadFromJsonAsync<IdBody>();
        return body?.Id ?? throw new ApplicationException("Could not publish the listing.");
    }

    public Task<MarketplaceItemDetailDto?> GetItemForEditAsync(string id) =>
        GetOrNullAsync<MarketplaceItemDetailDto>($"api/v1/marketplace/items/{id}/edit");

    public async Task<bool> UpdateItemAsync(string id, UpdateMarketplaceItemDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/marketplace/items/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<MyListingDto>> GetMyListingsAsync() =>
        await _httpClient.GetFromJsonAsync<List<MyListingDto>>("api/v1/marketplace/items/mine") ?? new List<MyListingDto>();

    public async Task<bool> MarkSoldAsync(string id, string? buyerInterestId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/marketplace/items/{id}/sold", new MarkSoldDto { BuyerInterestId = buyerInterestId });
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public Task<bool> DeleteItemAsync(string id) =>
        SendAsync(HttpMethod.Delete, $"api/v1/marketplace/items/{id}");

    public Task<PriceSuggestionDto?> GetPriceSuggestionAsync(MarketplaceCategory category, ItemCondition condition) =>
        GetOrNullAsync<PriceSuggestionDto>($"api/v1/marketplace/price-suggestion?category={category}&condition={condition}");

    // ---- Buy interests ----

    public async Task<bool> ExpressInterestAsync(string itemId, string message)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/v1/marketplace/items/{itemId}/interests", new ExpressInterestDto { Message = message });
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<BuyInterestDto>> GetItemInterestsAsync(string itemId) =>
        await _httpClient.GetFromJsonAsync<List<BuyInterestDto>>($"api/v1/marketplace/items/{itemId}/interests") ?? new List<BuyInterestDto>();

    public Task<bool> RespondToInterestAsync(string interestId, bool accept) =>
        SendAsync(HttpMethod.Post, $"api/v1/marketplace/interests/{interestId}/{(accept ? "accept" : "decline")}");

    public async Task<IReadOnlyList<MyBuyInterestDto>> GetMyBuyInterestsAsync() =>
        await _httpClient.GetFromJsonAsync<List<MyBuyInterestDto>>("api/v1/marketplace/interests/mine") ?? new List<MyBuyInterestDto>();

    public Task<bool> WithdrawInterestAsync(string interestId) =>
        SendAsync(HttpMethod.Post, $"api/v1/marketplace/interests/{interestId}/withdraw");

    // ---- Reports ----

    public async Task<(bool Ok, string Message)> ReportItemAsync(string itemId, string reason, string? details)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/marketplace/items/{itemId}/report", new ReportListingDto { Reason = reason, Details = details });
            var message = await ReadMessageAsync(response);
            return (response.IsSuccessStatusCode, message ?? (response.IsSuccessStatusCode ? "Report submitted." : "Could not send the report."));
        }
        catch (HttpRequestException)
        {
            return (false, "Could not reach the server.");
        }
    }

    // ---- helpers ----

    // 404 and 204 both mean "nothing there", which the pages show as not found.
    private async Task<T?> GetOrNullAsync<T>(string url) where T : class
    {
        var response = await _httpClient.GetAsync(url);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task<bool> SendAsync(HttpMethod method, string url)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<MessageBody>();
            return body?.Message;
        }
        catch
        {
            return null;
        }
    }

    private sealed class MessageBody
    {
        public string? Message { get; set; }
    }

    private sealed class IdBody
    {
        public string? Id { get; set; }
    }
}

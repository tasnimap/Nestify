// src/Nestify.Web/Services/Implementations/HousingService.cs
// The real IHousingService, talking to api/v1/housing (Housing.sql).
// The pages only see the DTOs, so nothing in them changes.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Nestify.Shared.Dtos.Housing;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class HousingService : IHousingService, IHouseLookupService
{
    private readonly HttpClient _httpClient;

    public HousingService(HttpClient httpClient) => _httpClient = httpClient;

    // ---- Houses ----

    public async Task<IReadOnlyList<HouseOptionDto>> GetManageableHousesAsync() =>
        await _httpClient.GetFromJsonAsync<List<HouseOptionDto>>("api/v1/housing/houses") ?? new List<HouseOptionDto>();

    // ---- Browse + detail ----

    public async Task<HousingPageDto<HousingPostSummaryDto>> BrowseAsync(HousingPostFilterDto filter)
    {
        var query = new List<string>
        {
            $"page={filter.Page}",
            $"pageSize={filter.PageSize}"
        };

        if (filter.DivisionId is { } division) query.Add($"divisionId={division}");
        if (filter.DistrictId is { } district) query.Add($"districtId={district}");
        if (filter.UpazilaId is { } upazila) query.Add($"upazilaId={upazila}");
        if (filter.ListingType is { } type) query.Add($"listingType={type}");
        if (filter.MaxRent is { } maxRent) query.Add("maxRent=" + maxRent.ToString(CultureInfo.InvariantCulture));

        var page = await _httpClient.GetFromJsonAsync<HousingPageDto<HousingPostSummaryDto>>(
            "api/v1/housing/posts?" + string.Join("&", query));
        return page ?? new HousingPageDto<HousingPostSummaryDto> { Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<HousingPostDetailDto?> GetPostAsync(string id) =>
        GetOrNullAsync<HousingPostDetailDto>($"api/v1/housing/posts/{id}");

    // ---- Create + edit + mine ----

    public async Task<string> CreateAsync(CreateHousingPostRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/housing/posts", request);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException(await ReadMessageAsync(response) ?? "Could not publish the listing.");
        }

        var body = await response.Content.ReadFromJsonAsync<IdBody>();
        return body?.Id ?? throw new ApplicationException("Could not publish the listing.");
    }

    public Task<HousingPostDetailDto?> GetPostForEditAsync(string id) =>
        GetOrNullAsync<HousingPostDetailDto>($"api/v1/housing/posts/{id}/edit");

    public async Task<bool> UpdateAsync(string id, UpdateHousingPostRequestDto request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/housing/posts/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<MyHousingPostDto>> GetMineAsync() =>
        await _httpClient.GetFromJsonAsync<List<MyHousingPostDto>>("api/v1/housing/posts/mine") ?? new List<MyHousingPostDto>();

    public Task<bool> CloseAsync(string id) => SendAsync(HttpMethod.Post, $"api/v1/housing/posts/{id}/close");

    public Task<bool> ReopenAsync(string id) => SendAsync(HttpMethod.Post, $"api/v1/housing/posts/{id}/reopen");

    public Task<bool> DeleteAsync(string id) => SendAsync(HttpMethod.Delete, $"api/v1/housing/posts/{id}");

    // ---- Bookings ----

    public async Task<bool> RequestBookingAsync(string postId, string? message)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/housing/posts/{postId}/bookings", new { message });
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<BookingRequesterDto>> GetRequestersAsync(string postId) =>
        await GetOrNullAsync<List<BookingRequesterDto>>($"api/v1/housing/posts/{postId}/bookings")
        ?? new List<BookingRequesterDto>();

    public Task<bool> AcceptBookingAsync(string bookingId) =>
        SendAsync(HttpMethod.Post, $"api/v1/housing/bookings/{bookingId}/accept");

    public async Task<bool> RejectBookingAsync(string bookingId, RejectBookingRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/housing/bookings/{bookingId}/reject", request);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public Task<ContactDisclosureDto?> GetBookingContactAsync(string bookingId) =>
        GetOrNullAsync<ContactDisclosureDto>($"api/v1/housing/bookings/{bookingId}/contact");

    public async Task<IReadOnlyList<MyBookingDto>> GetMyBookingsAsync() =>
        await _httpClient.GetFromJsonAsync<List<MyBookingDto>>("api/v1/housing/bookings/mine") ?? new List<MyBookingDto>();

    public Task<bool> WithdrawBookingAsync(string bookingId) =>
        SendAsync(HttpMethod.Post, $"api/v1/housing/bookings/{bookingId}/withdraw");

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

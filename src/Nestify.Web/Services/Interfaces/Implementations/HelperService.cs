using System.Net.Http.Json;
using Nestify.Shared.Dtos.Helpers;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class HelperService : IHelperService
{
    private readonly HttpClient _http;

    public HelperService(HttpClient http)
    {
        _http = http;
    }

    public async Task<HelperPageDto<HelperSummaryDto>> BrowseAsync(HelperFilterDto filter)
    {
        var query = BuildQuery(filter);
        var result = await _http.GetFromJsonAsync<HelperPageDto<HelperSummaryDto>>($"api/v1/helpers{query}");
        return result ?? new HelperPageDto<HelperSummaryDto>();
    }

    public async Task<HelperDetailDto?> GetHelperAsync(string id)
        => await _http.GetFromJsonAsync<HelperDetailDto>($"api/v1/helpers/{id}");

    public async Task<HelperDetailDto?> GetMyProfileAsync()
    {
        var response = await _http.GetAsync("api/v1/helpers/me");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<HelperDetailDto>();
    }

    public async Task<HelperDetailDto> RegisterAsync(HelperRegistrationDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/v1/helpers/me", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HelperDetailDto>())!;
    }

    public async Task<HelperDetailDto> UpdateProfileAsync(HelperRegistrationDto dto)
    {
        var response = await _http.PutAsJsonAsync("api/v1/helpers/me", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HelperDetailDto>())!;
    }

    public async Task<HelperPageDto<ReviewDto>> GetReviewsAsync(string helperId, int page = 1, int pageSize = 5)
    {
        var result = await _http.GetFromJsonAsync<HelperPageDto<ReviewDto>>(
            $"api/v1/helpers/{helperId}/reviews?page={page}&pageSize={pageSize}");
        return result ?? new HelperPageDto<ReviewDto>();
    }

    public async Task<List<EngagementDto>> GetMyEngagementsAsync()
    {
        var result = await _http.GetFromJsonAsync<List<EngagementDto>>("api/v1/helpers/engagements");
        return result ?? new List<EngagementDto>();
    }

    public async Task<EngagementDto> RequestEngagementAsync(string helperId)
    {
        var response = await _http.PostAsync($"api/v1/helpers/{helperId}/engagements", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EngagementDto>())!;
    }

    public async Task<EngagementDto> ConfirmEngagementAsync(string engagementId)
    {
        var response = await _http.PostAsync($"api/v1/helpers/engagements/{engagementId}/confirm", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EngagementDto>())!;
    }

    public async Task<EngagementDto> MarkCompleteAsync(string engagementId)
    {
        var response = await _http.PostAsync($"api/v1/helpers/engagements/{engagementId}/complete", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EngagementDto>())!;
    }

    public async Task SubmitReviewAsync(string engagementId, int rating, string comment)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/helpers/engagements/{engagementId}/review",
            new { Rating = rating, Comment = comment });
        response.EnsureSuccessStatusCode();
    }

    private static string BuildQuery(HelperFilterDto filter)
    {
        var parts = new List<string>();

        if (filter.DivisionId is not null) parts.Add($"DivisionId={filter.DivisionId}");
        if (filter.DistrictId is not null) parts.Add($"DistrictId={filter.DistrictId}");
        if (filter.UpazilaId is not null) parts.Add($"UpazilaId={filter.UpazilaId}");
        if (filter.ServiceType is not null) parts.Add($"ServiceType={filter.ServiceType}");
        if (filter.MaxMonthlyRate is not null) parts.Add($"MaxMonthlyRate={filter.MaxMonthlyRate}");
        if (filter.MinRating is not null) parts.Add($"MinRating={filter.MinRating}");
        parts.Add($"Sort={filter.Sort}");
        parts.Add($"Page={filter.Page}");
        parts.Add($"PageSize={filter.PageSize}");

        return "?" + string.Join("&", parts);
    }
}
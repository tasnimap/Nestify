using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Nestify.Shared.Dtos.Helpers;
using Nestify.Shared.Dtos.Profile;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class HelperService : IHelperService
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly HttpClient _http;

    public HelperService(HttpClient http)
    {
        _http = http;
    }

    // ------------------------------------------------------------ browsing and booking

    public async Task<HelperPageDto<HelperSummaryDto>> BrowseAsync(HelperFilterDto filter)
        => await _http.GetFromJsonAsync<HelperPageDto<HelperSummaryDto>>($"api/v1/helpers{BuildQuery(filter)}")
           ?? new HelperPageDto<HelperSummaryDto>();

    public async Task<HelperDetailDto?> GetHelperAsync(string id)
    {
        var response = await _http.GetAsync($"api/v1/helpers/{id}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<HelperDetailDto>() : null;
    }

    public async Task<HelperPageDto<ReviewDto>> GetReviewsAsync(string helperId, int page = 1, int pageSize = 5)
        => await _http.GetFromJsonAsync<HelperPageDto<ReviewDto>>($"api/v1/helpers/{helperId}/reviews?page={page}&pageSize={pageSize}")
           ?? new HelperPageDto<ReviewDto>();

    public async Task<List<EngagementDto>> GetMyEngagementsAsync()
        => await _http.GetFromJsonAsync<List<EngagementDto>>("api/v1/helpers/engagements") ?? new List<EngagementDto>();

    public async Task<EngagementDto> RequestEngagementAsync(string helperId, EngagementRequestDto request)
    {
        var response = await _http.PostAsJsonAsync($"api/v1/helpers/{helperId}/engagements", request);
        await ThrowIfFailedAsync(response, "Could not send the request.");
        return (await response.Content.ReadFromJsonAsync<EngagementDto>())!;
    }

    public async Task CancelRequestAsync(string engagementId)
        => await ThrowIfFailedAsync(await _http.PostAsync($"api/v1/helpers/engagements/{engagementId}/cancel", null), "Could not withdraw the request.");

    public async Task RejectRequestAsync(string engagementId)
        => await ThrowIfFailedAsync(await _http.PostAsync($"api/v1/helpers/engagements/{engagementId}/reject", null), "Could not reject the request.");

    public async Task MarkCompleteAsync(string engagementId)
        => await ThrowIfFailedAsync(await _http.PostAsync($"api/v1/helpers/engagements/{engagementId}/complete", null), "Could not mark the engagement complete.");

    public async Task ReleaseEngagementAsync(string engagementId)
        => await ThrowIfFailedAsync(await _http.PostAsync($"api/v1/helpers/engagements/{engagementId}/release", null), "Could not release the helper.");

    public async Task SubmitReviewAsync(string engagementId, int rating, string comment)
        => await ThrowIfFailedAsync(
            await _http.PostAsJsonAsync($"api/v1/helpers/engagements/{engagementId}/review", new SubmitReviewDto { Rating = rating, Comment = comment }),
            "Could not post the review.");

    // ------------------------------------------------------------ own profile

    public async Task<HelperProfileDto?> GetMyProfileAsync()
    {
        var response = await _http.GetAsync("api/v1/helpers/me");
        await ThrowIfFailedAsync(response, $"The API answered {(int)response.StatusCode} {response.ReasonPhrase}.");
        return await response.Content.ReadFromJsonAsync<HelperProfileDto>();
    }

    public async Task<HelperNavDto?> GetNavAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<HelperNavDto>("api/v1/helpers/me/nav");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<HelperProfileDto> UpdateProfileAsync(HelperProfileFormDto form)
    {
        var response = await _http.PutAsJsonAsync("api/v1/helpers/me", form);
        await ThrowIfFailedAsync(response, "Could not save your profile.");
        return (await response.Content.ReadFromJsonAsync<HelperProfileDto>())!;
    }

    public async Task<HelperProfileDto> UploadPhotoAsync(IBrowserFile file)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(MaxUploadBytes);
        form.Add(FilePart(stream, file.ContentType), "file", file.Name);

        var response = await _http.PostAsync("api/v1/helpers/me/photo", form);
        await ThrowIfFailedAsync(response, "Could not upload the photo.");
        return (await response.Content.ReadFromJsonAsync<HelperProfileDto>())!;
    }

    // ------------------------------------------------------------ verification

    public async Task<HelperVerificationStatusDto?> GetVerificationStatusAsync()
    {
        var response = await _http.GetAsync("api/v1/helpers/me/verification");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<HelperVerificationStatusDto>() : null;
    }

    public async Task<decimal> GetVerificationFeeAsync()
    {
        var fee = await _http.GetFromJsonAsync<VerificationFeeDto>("api/v1/helpers/me/verification/fee");
        return fee?.AmountBdt ?? 0;
    }

    public async Task<VerificationPaymentDto> PayVerificationFeeAsync(string bkashNumber, string pin)
    {
        var response = await _http.PostAsJsonAsync("api/v1/helpers/me/verification/payment",
            new BkashPaymentDto { BkashNumber = bkashNumber, Pin = pin });
        await ThrowIfFailedAsync(response, "The bKash payment did not go through.");
        return (await response.Content.ReadFromJsonAsync<VerificationPaymentDto>())!;
    }

    public async Task SubmitVerificationAsync(IBrowserFile photo, IBrowserFile nid, string paymentId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(paymentId), "paymentId");

        await using var photoStream = photo.OpenReadStream(MaxUploadBytes);
        await using var nidStream = nid.OpenReadStream(MaxUploadBytes);
        form.Add(FilePart(photoStream, photo.ContentType), "photoFile", photo.Name);
        form.Add(FilePart(nidStream, nid.ContentType), "nidFile", nid.Name);

        await ThrowIfFailedAsync(await _http.PostAsync("api/v1/helpers/me/verification", form), "Could not send the application.");
    }

    public async Task SubmitVerificationAsync(byte[] photoBytes, string photoContentType, string photoName,
        byte[] nidBytes, string nidContentType, string nidName, string paymentId)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(paymentId), "paymentId");

        form.Add(FilePart(new MemoryStream(photoBytes), photoContentType), "photoFile", photoName);
        form.Add(FilePart(new MemoryStream(nidBytes), nidContentType), "nidFile", nidName);

        await ThrowIfFailedAsync(await _http.PostAsync("api/v1/helpers/me/verification", form), "Could not send the application.");
    }

    public async Task CancelVerificationAsync()
        => await ThrowIfFailedAsync(await _http.DeleteAsync("api/v1/helpers/me/verification"), "Could not withdraw the application.");

    // ------------------------------------------------------------ workspace

    public Task<HelperWorkspaceDashboardDto?> GetWorkspaceDashboardAsync()
        => GetOrNullAsync<HelperWorkspaceDashboardDto>("api/v1/helpers/me/workspace/dashboard");

    public Task<HelperAvailabilityDto?> GetAvailabilityAsync()
        => GetOrNullAsync<HelperAvailabilityDto>("api/v1/helpers/me/workspace/availability");

    public async Task SaveAvailabilityAsync(HelperAvailabilityDto availability)
        => await ThrowIfFailedAsync(await _http.PutAsJsonAsync("api/v1/helpers/me/workspace/availability", availability), "Could not save your hours.");

    public Task<HelperWorkspaceScheduleDto?> GetWorkspaceScheduleAsync(DateTime weekStart)
        => GetOrNullAsync<HelperWorkspaceScheduleDto>(
            $"api/v1/helpers/me/workspace/schedule?weekStart={weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

    public Task<HelperWorkspaceEngagementsDto?> GetWorkspaceEngagementsAsync()
        => GetOrNullAsync<HelperWorkspaceEngagementsDto>("api/v1/helpers/me/workspace/engagements");

    public async Task AcceptWorkspaceEngagementAsync(string id)
        => await ThrowIfFailedAsync(await _http.PostAsync($"api/v1/helpers/me/workspace/engagements/{id}/accept", null), "Could not accept the request.");

    public async Task DeclineWorkspaceEngagementAsync(string id, string? reason)
        => await ThrowIfFailedAsync(
            await _http.PostAsJsonAsync($"api/v1/helpers/me/workspace/engagements/{id}/decline", new DeclineEngagementDto { Reason = reason }),
            "Could not decline the request.");

    public Task<HelperReviewsDto?> GetMyReviewsAsync()
        => GetOrNullAsync<HelperReviewsDto>("api/v1/helpers/me/workspace/reviews");

    public async Task ReplyToReviewAsync(string reviewId, string reply)
        => await ThrowIfFailedAsync(
            await _http.PostAsJsonAsync($"api/v1/helpers/me/workspace/reviews/{reviewId}/reply", new ReviewReplyDto { Reply = reply }),
            "Could not post the reply.");

    // ------------------------------------------------------------ plumbing

    private async Task<T?> GetOrNullAsync<T>(string url) where T : class
    {
        var response = await _http.GetAsync(url);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : null;
    }

    private static StreamContent FilePart(Stream stream, string? contentType)
    {
        var part = new StreamContent(stream);
        part.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        return part;
    }

    private static async Task ThrowIfFailedAsync(HttpResponseMessage response, string fallback)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var message = fallback;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("message", out var text) && !string.IsNullOrWhiteSpace(text.GetString()))
                {
                    message = text.GetString()!;
                }
            }
            catch (JsonException)
            {
                // Not JSON, keep the fallback.
            }
        }

        throw new ApplicationException(message);
    }

    private static string BuildQuery(HelperFilterDto filter)
    {
        var parts = new List<string>();
        if (filter.DivisionId is not null) parts.Add($"DivisionId={filter.DivisionId}");
        if (filter.DistrictId is not null) parts.Add($"DistrictId={filter.DistrictId}");
        if (filter.UpazilaId is not null) parts.Add($"UpazilaId={filter.UpazilaId}");
        if (filter.ServiceType is not null) parts.Add($"ServiceType={filter.ServiceType}");
        if (filter.MaxMonthlyRate is not null) parts.Add($"MaxMonthlyRate={filter.MaxMonthlyRate.Value.ToString(CultureInfo.InvariantCulture)}");
        if (filter.MinRating is not null) parts.Add($"MinRating={filter.MinRating.Value.ToString(CultureInfo.InvariantCulture)}");
        if (filter.VerifiedOnly) parts.Add("VerifiedOnly=true");
        parts.Add($"Sort={filter.Sort}");
        parts.Add($"Page={filter.Page}");
        parts.Add($"PageSize={filter.PageSize}");
        return "?" + string.Join("&", parts);
    }
}

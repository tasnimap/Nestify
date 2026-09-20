// The real IAdminService, talking to api/v1/admin (Admin.sql).
using System.Net.Http.Json;
using Nestify.Shared.Dtos.Admin;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class AdminService : IAdminService
{
    private readonly HttpClient _http;

    public AdminService(HttpClient http) => _http = http;

    public async Task<AdminSummaryDto> GetSummaryAsync() =>
        await GetAsync<AdminSummaryDto>("api/v1/admin/summary") ?? new AdminSummaryDto();

    // ---- Housing ----

    public async Task<List<AdminHousingPostDto>> GetHousingPostsAsync() =>
        await GetAsync<List<AdminHousingPostDto>>("api/v1/admin/housing/posts") ?? new();

    public async Task<List<AdminReportDto>> GetHousingReportsAsync() =>
        await GetAsync<List<AdminReportDto>>("api/v1/admin/housing/reports") ?? new();

    public Task<(bool Ok, string Message)> TakeDownHousingPostAsync(string id, string reason, string? reportId) =>
        PostAsync($"api/v1/admin/housing/posts/{id}/takedown", new TakedownRequestDto { Reason = reason, ReportId = reportId });

    public Task<(bool Ok, string Message)> RestoreHousingPostAsync(string id) =>
        PostAsync($"api/v1/admin/housing/posts/{id}/restore", null);

    // ---- Marketplace ----

    public async Task<List<AdminMarketItemDto>> GetMarketItemsAsync() =>
        await GetAsync<List<AdminMarketItemDto>>("api/v1/admin/marketplace/items") ?? new();

    public async Task<List<AdminReportDto>> GetMarketReportsAsync() =>
        await GetAsync<List<AdminReportDto>>("api/v1/admin/marketplace/reports") ?? new();

    public Task<(bool Ok, string Message)> TakeDownMarketItemAsync(string id, string reason, string? reportId) =>
        PostAsync($"api/v1/admin/marketplace/items/{id}/takedown", new TakedownRequestDto { Reason = reason, ReportId = reportId });

    public Task<(bool Ok, string Message)> RestoreMarketItemAsync(string id) =>
        PostAsync($"api/v1/admin/marketplace/items/{id}/restore", null);

    // ---- Reports ----

    public async Task<bool> DismissReportAsync(ModerationScope scope, string id) =>
        (await PostAsync($"api/v1/admin/reports/{scope}/{id}/dismiss", null)).Ok;

    // ---- Fees and plans ----

    public async Task<List<AdminFeeDto>> GetFeesAsync() =>
        await GetAsync<List<AdminFeeDto>>("api/v1/admin/fees") ?? new();

    public async Task<(bool Ok, string Message)> SaveFeeAsync(string code, decimal amount)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"api/v1/admin/fees/{code}", new SaveFeeDto { Amount = amount });
            return (response.IsSuccessStatusCode, await ReadMessageAsync(response) ?? "Fee saved.");
        }
        catch (HttpRequestException)
        {
            return (false, "Could not reach the server.");
        }
    }

    public async Task<List<AdminPlanDto>> GetPlansAsync() =>
        await GetAsync<List<AdminPlanDto>>("api/v1/admin/plans") ?? new();

    public Task<(bool Ok, string Message)> CreatePlanAsync(SavePlanDto dto) =>
        PostAsync("api/v1/admin/plans", dto);

    public async Task<(bool Ok, string Message)> UpdatePlanAsync(string id, SavePlanDto dto)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"api/v1/admin/plans/{id}", dto);
            return (response.IsSuccessStatusCode, await ReadMessageAsync(response) ?? "Plan saved.");
        }
        catch (HttpRequestException)
        {
            return (false, "Could not reach the server.");
        }
    }

    public async Task<bool> TogglePlanAsync(string id) =>
        (await PostAsync($"api/v1/admin/plans/{id}/toggle", null)).Ok;

    public async Task<(bool Ok, string Message)> DeletePlanAsync(string id)
    {
        try
        {
            var response = await _http.DeleteAsync($"api/v1/admin/plans/{id}");
            return (response.IsSuccessStatusCode, await ReadMessageAsync(response) ?? "Plan deleted.");
        }
        catch (HttpRequestException)
        {
            return (false, "Could not reach the server.");
        }
    }

    // ---- Admin accounts ----

    public async Task<List<AdminAccountDto>> GetAdminsAsync() =>
        await GetAsync<List<AdminAccountDto>>("api/v1/admin/accounts") ?? new();

    public Task<(bool Ok, string Message)> CreateAdminAsync(CreateAdminDto dto) =>
        PostAsync("api/v1/admin/accounts", dto);

    public Task<(bool Ok, string Message)> ToggleAdminAsync(string id) =>
        PostAsync($"api/v1/admin/accounts/{id}/toggle", null);

    // ---- Audit ----

    public async Task<List<AdminAuditEntryDto>> GetAuditAsync(int take = 200) =>
        await GetAsync<List<AdminAuditEntryDto>>($"api/v1/admin/audit?take={take}") ?? new();

    // ---- Verification ----

    public async Task<List<VerificationRequestDto>> GetVerificationsAsync() =>
        await GetAsync<List<VerificationRequestDto>>("api/v1/admin/verifications") ?? new();

    public Task<(bool Ok, string Message)> DecideVerificationAsync(string id, bool approve, string? reason) =>
        PostAsync($"api/v1/admin/verifications/{id}/decision", new VerificationDecisionDto { Approve = approve, Reason = reason });

    // ---- helpers ----

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        try
        {
            var response = await _http.GetAsync(url);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<(bool Ok, string Message)> PostAsync(string url, object? body)
    {
        try
        {
            var response = body is null
                ? await _http.PostAsync(url, null)
                : await _http.PostAsJsonAsync(url, body);
            var message = await ReadMessageAsync(response);
            return (response.IsSuccessStatusCode, message ?? (response.IsSuccessStatusCode
                ? "Done."
                : $"The server answered {(int)response.StatusCode}. Is Admin.sql loaded?"));
        }
        catch (HttpRequestException)
        {
            return (false, "Could not reach the server.");
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
}

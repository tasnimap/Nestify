// The real ISettlementService, talking to api/v1/settlement (Settlement.sql).
using System.Net;
using System.Net.Http.Json;
using Nestify.Shared.Dtos.Settlement;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class SettlementService : ISettlementService
{
    private readonly HttpClient _httpClient;

    public SettlementService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<SettlementBookDto>?> GetBooksAsync() =>
        await GetOrNullAsync<List<SettlementBookDto>>("api/v1/settlement/mine/books");

    public async Task<IReadOnlyList<MonthlyMemberMealCostDto>?> GetMealCostHistoryAsync() =>
        await GetOrNullAsync<List<MonthlyMemberMealCostDto>>("api/v1/settlement/mine/meal-cost-history");

    public Task<SettlementWorkspaceDto?> GetAsync(int year, int month) =>
        GetOrNullAsync<SettlementWorkspaceDto>(Url("api/v1/settlement/mine", year, month));

    public Task<(bool Ok, string Message)> OpenBookAsync(int year, int month) =>
        SendAsync(HttpMethod.Post, Url("api/v1/settlement/mine/open", year, month));

    public Task<(bool Ok, string Message)> AddMemberAsync(int year, int month, long userId) =>
        PostAsync(Url("api/v1/settlement/mine/members", year, month), new AddSettlementMemberRequest { UserId = userId });

    public async Task<(SettlementBillDto? Data, string? Error)> AddBillAsync(int year, int month, CreateSettlementBillRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(Url("api/v1/settlement/mine/bills", year, month), request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadMessageAsync(response) ?? "Could not add the bill.");
        }

        return (await response.Content.ReadFromJsonAsync<SettlementBillDto>(), null);
    }

    public async Task<(bool Ok, string Message)> UpdateBillAsync(int year, int month, long billId, decimal amount)
    {
        var response = await _httpClient.PutAsJsonAsync(
            Url($"api/v1/settlement/mine/bills/{billId}", year, month), new UpdateSettlementBillRequest { Amount = amount });
        return await ToResultAsync(response);
    }

    public Task<(bool Ok, string Message)> DeleteBillAsync(int year, int month, long billId) =>
        SendAsync(HttpMethod.Delete, Url($"api/v1/settlement/mine/bills/{billId}", year, month));

    public async Task<(SettlementPaymentDto? Data, string? Error)> AddPaymentAsync(int year, int month, CreateSettlementPaymentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync(Url("api/v1/settlement/mine/payments", year, month), request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadMessageAsync(response) ?? "Could not record the payment.");
        }

        return (await response.Content.ReadFromJsonAsync<SettlementPaymentDto>(), null);
    }

    public Task<(bool Ok, string Message)> DeletePaymentAsync(int year, int month, long paymentId) =>
        SendAsync(HttpMethod.Delete, Url($"api/v1/settlement/mine/payments/{paymentId}", year, month));

    public async Task<(bool Ok, string Message)> SaveMealsAsync(int year, int month, SaveSettlementMealsRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync(Url("api/v1/settlement/mine/meals", year, month), request);
        return await ToResultAsync(response);
    }

    public Task<(bool Ok, string Message)> FinalizeAsync(int year, int month) =>
        SendAsync(HttpMethod.Post, Url("api/v1/settlement/mine/finalize", year, month));

    // ---- helpers ----

    private static string Url(string path, int year, int month) => $"{path}?year={year}&month={month}";

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

    private async Task<(bool Ok, string Message)> PostAsync<T>(string url, T body)
    {
        var response = await _httpClient.PostAsJsonAsync(url, body);
        return await ToResultAsync(response);
    }

    private async Task<(bool Ok, string Message)> SendAsync(HttpMethod method, string url)
    {
        using var request = new HttpRequestMessage(method, url);
        var response = await _httpClient.SendAsync(request);
        return await ToResultAsync(response);
    }

    private static async Task<(bool Ok, string Message)> ToResultAsync(HttpResponseMessage response)
    {
        var message = await ReadMessageAsync(response);
        return response.IsSuccessStatusCode
            ? (true, message ?? "Done.")
            : (false, message ?? "Something went wrong.");
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

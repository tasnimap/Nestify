using System.Net.Http.Json;
using Nestify.Shared.Dtos.Admin;
using Nestify.Web.Services.Interfaces;
namespace Nestify.Web.Services.Implementations;
public sealed class VerificationAdminService : IVerificationAdminService
{
    private readonly HttpClient _http;
    public VerificationAdminService(HttpClient http) => _http = http;
    public async Task<List<VerificationRequestDto>> GetQueueAsync() => await _http.GetFromJsonAsync<List<VerificationRequestDto>>("api/v1/admin/verifications") ?? new();
    public async Task DecideAsync(string id, bool approve) { var response = await _http.PostAsJsonAsync($"api/v1/admin/verifications/{id}/decision", new { approve }); response.EnsureSuccessStatusCode(); }
}

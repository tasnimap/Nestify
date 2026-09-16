using System.Net.Http.Json;
using Nestify.Shared.Dtos.Admin;

namespace Nestify.Web.Admin;

// The one admin page that is not mock data: it reads the signed-in admin's own
// row through api/v1/admin/me.
public sealed class AdminProfileClient
{
    private readonly HttpClient _http;

    public AdminProfileClient(HttpClient http) => _http = http;

    public async Task<AdminProfileDto?> GetAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/v1/admin/me");
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<AdminProfileDto>()
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

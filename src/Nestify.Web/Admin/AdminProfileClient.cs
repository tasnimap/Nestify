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

    public async Task<(bool Ok, string Message)> UpdateAsync(UpdateAdminProfileDto dto)
    {
        try
        {
            var response = await _http.PutAsJsonAsync("api/v1/admin/me", dto);
            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }

            var message = "Could not update the profile.";
            try
            {
                var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                if (body.TryGetProperty("message", out var text) && !string.IsNullOrWhiteSpace(text.GetString()))
                {
                    message = text.GetString()!;
                }
            }
            catch
            {
                // Keep the fallback message.
            }
            return (false, message);
        }
        catch (HttpRequestException)
        {
            return (false, "The server could not be reached.");
        }
    }
}

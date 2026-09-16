using System.Net.Http.Json;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Web.Maid;

// The one helper page that is not sample data: it reads the signed-in helper's
// own row through api/v1/helpers/me/account.
public sealed class MaidAccountClient
{
    private readonly HttpClient _http;

    public MaidAccountClient(HttpClient http) => _http = http;

    public async Task<HelperAccountDto?> GetAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/v1/helpers/me/account");
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<HelperAccountDto>()
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

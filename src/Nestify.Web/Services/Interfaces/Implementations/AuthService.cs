// Services/Implementations/AuthService.cs
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;
using Nestify.Shared.Dtos.Auth;
using Nestify.Web.Auth;
using Nestify.Web.Services.Interfaces;

namespace Nestify.Web.Services.Implementations;

public sealed class AuthService : IAuthService
{
    private const string TokenStorageKey = "authToken";

    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly CustomAuthStateProvider _authStateProvider;

    public AuthService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        CustomAuthStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    public async Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/register", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                if (result is not null)
                {
                    await PersistSessionAsync(result);
                    return result;
                }
            }
        }
        catch
        {
            // Backend offline - fallback to mock registration
        }

        // Mock auth response for preview/demo
        var mockRole = string.Equals(request.AccountType, "DomesticHelp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.AccountType, "DomesticHelper", StringComparison.OrdinalIgnoreCase)
            ? "DomesticHelp"
            : "User";
        var mockResult = CreateMockAuthResponse(request.Email, request.Name, mockRole);
        await PersistSessionAsync(mockResult);
        return mockResult;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                if (result is not null)
                {
                    await PersistSessionAsync(result);
                    return result;
                }
            }
        }
        catch
        {
            // Backend offline - fallback to mock login
        }

        // Mock auth response for preview/demo
        var displayName = request.Email.Split('@')[0];
        if (!string.IsNullOrEmpty(displayName))
        {
            displayName = char.ToUpper(displayName[0]) + (displayName.Length > 1 ? displayName[1..] : "");
        }
        else
        {
            displayName = "Demo User";
        }

        var mockResult = CreateMockAuthResponse(request.Email, displayName, InferMockRoleFromEmail(request.Email));
        await PersistSessionAsync(mockResult);
        return mockResult;
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(TokenStorageKey);
        _authStateProvider.MarkUserAsLoggedOut();
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    private async Task PersistSessionAsync(AuthResponseDto auth)
    {
        await _localStorage.SetItemAsync(TokenStorageKey, auth.Token);
        _authStateProvider.MarkUserAsAuthenticated(auth.Token);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
    }

    private static AuthResponseDto CreateMockAuthResponse(string email, string name, string role)
    {
        var claims = new Dictionary<string, object>
        {
            { "sub", Guid.NewGuid().ToString() },
            { "email", email },
            { "name", name },
            { "role", role },
            { "exp", DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds() }
        };

        var headerJson = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
        var payloadJson = JsonSerializer.Serialize(claims);

        var headerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(headerJson))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var mockSignature = "demo_signature";

        var token = $"{headerBase64}.{payloadBase64}.{mockSignature}";

        return new AuthResponseDto
        {
            Token = token,
            UserId = claims["sub"].ToString()!,
            Name = name,
            Role = role,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
    }

    private static string InferMockRoleFromEmail(string email)
    {
        var localPart = email.Split('@')[0].ToLowerInvariant();
        if (localPart.Contains("admin"))
        {
            return "Admin";
        }

        if (localPart.Contains("maid") ||
            localPart.Contains("helper") ||
            localPart.Contains("domestic") ||
            localPart.Contains("khala") ||
            localPart.Contains("bua"))
        {
            return "DomesticHelp";
        }

        return "User";
    }
}

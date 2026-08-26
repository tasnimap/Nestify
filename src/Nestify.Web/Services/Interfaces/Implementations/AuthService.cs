// Services/Implementations/AuthService.cs
using System.Net.Http.Json;
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
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/register", request);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        if (result is not null) await PersistSessionAsync(result);
        return result;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", request);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        if (result is not null) await PersistSessionAsync(result);
        return result;
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
}
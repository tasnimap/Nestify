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
    private const string RefreshStorageKey = "refreshToken";

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
        => await SendAsync("api/v1/auth/register", request);

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
        => await SendAsync("api/v1/auth/login", request);

    public async Task LogoutAsync()
    {
        var refreshToken = await _localStorage.GetItemAsync<string>(RefreshStorageKey);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                await _httpClient.PostAsJsonAsync("api/v1/auth/logout",
                    new RefreshRequestDto { RefreshToken = refreshToken });
            }
            catch
            {
                // Best effort - clear the local session regardless.
            }
        }

        await _localStorage.RemoveItemAsync(TokenStorageKey);
        await _localStorage.RemoveItemAsync(RefreshStorageKey);
        _authStateProvider.MarkUserAsLoggedOut();
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    private async Task<AuthResponseDto?> SendAsync<TRequest>(string url, TRequest request)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(url, request);
        }
        catch (HttpRequestException)
        {
            throw new ApplicationException("We could not reach Nestify right now. Try again in a moment.");
        }

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (result is not null)
            {
                await PersistSessionAsync(result);
                return result;
            }

            throw new ApplicationException("The server returned an unexpected response.");
        }

        var problem = await TryReadMessageAsync(response);
        throw new ApplicationException(problem ?? "Something went wrong. Please try again.");
    }

    private static async Task<string?> TryReadMessageAsync(HttpResponseMessage response)
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

    private async Task PersistSessionAsync(AuthResponseDto auth)
    {
        await _localStorage.SetItemAsync(TokenStorageKey, auth.Token);
        await _localStorage.SetItemAsync(RefreshStorageKey, auth.RefreshToken);
        _authStateProvider.MarkUserAsAuthenticated(auth.Token);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
    }

    private sealed class MessageBody
    {
        public string? Message { get; set; }
    }
}

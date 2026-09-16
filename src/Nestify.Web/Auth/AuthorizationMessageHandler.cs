// Auth/AuthorizationMessageHandler.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Nestify.Shared.Dtos.Auth;

namespace Nestify.Web.Auth;

// Attaches the stored access token to every request the app makes to the API.
//
// The access token only lasts a couple of hours, so a tab left open since the
// evening used to come back with an expired token: the app still looked logged
// in, the API answered 401 and every page reported its own generic failure
// ("Could not load your home"). The refresh token is good for a week, so the
// handler spends it here — before the call when the token has already run out,
// and once more if the API rejects the token anyway. Only when that fails is
// the session really over, and then we clear it and go back to /auth.
public sealed class AuthorizationMessageHandler : DelegatingHandler
{
    private const string TokenStorageKey = "authToken";
    private const string RefreshStorageKey = "refreshToken";

    // One refresh at a time, or the first few calls of a page each burn a
    // refresh token and the losers get logged out.
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    private readonly ILocalStorageService _localStorage;
    private readonly CustomAuthStateProvider _authStateProvider;
    private readonly NavigationManager _navigation;
    private readonly string _apiBaseUrl;

    public AuthorizationMessageHandler(
        ILocalStorageService localStorage,
        CustomAuthStateProvider authStateProvider,
        NavigationManager navigation,
        string apiBaseUrl)
    {
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
        _navigation = navigation;
        _apiBaseUrl = apiBaseUrl;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is not null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var token = await _localStorage.GetItemAsync<string>(TokenStorageKey, cancellationToken);

        if (!string.IsNullOrWhiteSpace(token) && JwtToken.IsExpired(token))
        {
            token = await RefreshAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Buffered so the same body can go out again on the retry below.
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync();
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || string.IsNullOrWhiteSpace(token))
        {
            return response;
        }

        var refreshed = await RefreshAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(refreshed))
        {
            await SignOutAsync();
            return response;
        }

        response.Dispose();
        using var retry = await CloneAsync(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(retry, cancellationToken);
    }

    // Returns the new access token, or null when the session cannot be renewed.
    private async Task<string?> RefreshAsync(CancellationToken cancellationToken)
    {
        await RefreshGate.WaitAsync(cancellationToken);
        try
        {
            // Someone else may have renewed it while we waited.
            var current = await _localStorage.GetItemAsync<string>(TokenStorageKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(current) && !JwtToken.IsExpired(current))
            {
                return current;
            }

            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshStorageKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            // A plain client: going through this handler would recurse.
            using var client = new HttpClient { BaseAddress = new Uri(_apiBaseUrl) };
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync(
                    "api/v1/auth/refresh",
                    new RefreshRequestDto { RefreshToken = refreshToken },
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                return null;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken);
                if (auth is null || string.IsNullOrWhiteSpace(auth.Token))
                {
                    return null;
                }

                await _localStorage.SetItemAsync(TokenStorageKey, auth.Token, cancellationToken);
                await _localStorage.SetItemAsync(RefreshStorageKey, auth.RefreshToken, cancellationToken);
                _authStateProvider.MarkUserAsAuthenticated(auth.Token);
                return auth.Token;
            }
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private async Task SignOutAsync()
    {
        await _localStorage.RemoveItemAsync(TokenStorageKey);
        await _localStorage.RemoveItemAsync(RefreshStorageKey);
        _authStateProvider.MarkUserAsLoggedOut();
        _navigation.NavigateTo("/auth", replace: true);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(body);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}

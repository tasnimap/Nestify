// Auth/CustomAuthStateProvider.cs
using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace Nestify.Web.Auth;

public sealed class CustomAuthStateProvider : AuthenticationStateProvider
{
    private const string TokenStorageKey = "authToken";
    private const string RefreshStorageKey = "refreshToken";
    private readonly ILocalStorageService _localStorage;

    public CustomAuthStateProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _localStorage.GetItemAsync<string>(TokenStorageKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Anonymous();
        }

        // An expired access token is still a live session as long as the refresh
        // token is there: AuthorizationMessageHandler swaps it on the next call.
        // Without one there is nothing left to renew, so treat it as logged out
        // instead of letting the app run into 401s.
        if (JwtToken.IsExpired(token))
        {
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshStorageKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                await _localStorage.RemoveItemAsync(TokenStorageKey);
                return Anonymous();
            }
        }

        try
        {
            var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Anonymous();
        }
    }

    public void MarkUserAsAuthenticated(string token)
    {
        try
        {
            var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
            var user = new ClaimsPrincipal(identity);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }
        catch
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Demo User") }, "jwt");
            var user = new ClaimsPrincipal(identity);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }
    }

    public void MarkUserAsLoggedOut() =>
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));

    private static AuthenticationState Anonymous() =>
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = JwtToken.DecodeBase64Url(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes)!;

        return keyValuePairs.SelectMany(kvp => CreateClaims(kvp.Key, kvp.Value));
    }

    private static IEnumerable<Claim> CreateClaims(string sourceType, JsonElement value)
    {
        var claimType = sourceType switch
        {
            "name" => ClaimTypes.Name,
            "email" => ClaimTypes.Email,
            "role" => ClaimTypes.Role,
            _ => sourceType
        };

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                yield return new Claim(claimType, item.ToString());
            }

            yield break;
        }

        yield return new Claim(claimType, value.ToString());
    }
}

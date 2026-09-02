// Auth/CustomAuthStateProvider.cs
using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace Nestify.Web.Auth;

public sealed class CustomAuthStateProvider : AuthenticationStateProvider
{
    private const string TokenStorageKey = "authToken";
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
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        try
        {
            var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
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

    public void MarkUserAsLoggedOut()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymous)));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
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

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}

// Auth/JwtToken.cs
using System.Text.Json;

namespace Nestify.Web.Auth;

// Reading the bits of the access token the client needs. The signature is the
// API's business; all the app wants to know is when the token stops working.
public static class JwtToken
{
    // A small margin so a token that dies mid-flight is renewed before the call.
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);

    public static bool IsExpired(string? jwt)
    {
        var expiry = ExpiresAtUtc(jwt);
        return expiry is null || expiry.Value - Skew <= DateTime.UtcNow;
    }

    public static DateTime? ExpiresAtUtc(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                DecodeBase64Url(parts[1]));

            if (payload is null || !payload.TryGetValue("exp", out var exp))
            {
                return null;
            }

            var seconds = exp.ValueKind == JsonValueKind.Number
                ? exp.GetInt64()
                : long.Parse(exp.ToString());

            return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    public static byte[] DecodeBase64Url(string value)
    {
        value = value.Replace('-', '+').Replace('_', '/');
        switch (value.Length % 4)
        {
            case 2: value += "=="; break;
            case 3: value += "="; break;
        }

        return Convert.FromBase64String(value);
    }
}

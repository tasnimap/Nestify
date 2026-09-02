namespace Nestify.Api.Auth;

// Non-secret JWT config comes from appsettings; the signing key comes from .env.
public sealed class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 120;
    public int RefreshTokenDays { get; set; } = 7;
}

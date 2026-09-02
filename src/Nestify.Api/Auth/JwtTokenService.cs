using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Nestify.Api.Auth;

public sealed class JwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(JwtSettings settings)
    {
        _settings = settings;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(long userId, string name, string email, string role)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("name", name),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    // A refresh token is just random bytes handed to the client; the DB only keeps its SHA-256 hash.
    public static (string Raw, byte[] Hash) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = Convert.ToBase64String(bytes);
        return (raw, SHA256.HashData(bytes));
    }

    public static byte[] HashRefreshToken(string raw)
    {
        return SHA256.HashData(Convert.FromBase64String(raw));
    }
}

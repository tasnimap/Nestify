using System.Data;
using System.Net;
using Dapper;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Auth;

namespace Nestify.Api.Auth;

public sealed class AuthService
{
    // account_type on the users row decides which interface the client shows.
    private const short AccountUser = 1;
    private const short AccountDomesticHelper = 2;
    private const short AccountAdmin = 3;

    private readonly DbConnectionFactory _db;
    private readonly JwtTokenService _tokens;
    private readonly JwtSettings _settings;

    public AuthService(DbConnectionFactory db, JwtTokenService tokens, JwtSettings settings)
    {
        _db = db;
        _tokens = tokens;
        _settings = settings;
    }

    public async Task<(AuthResponseDto? Data, string? Error)> RegisterAsync(RegisterRequestDto request, string? ip)
    {
        var name = request.Name.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = request.Phone.Trim();

        if (name.Length < 2 || email.Length == 0 || request.Password.Length < 8)
        {
            return (null, "Name, email and an 8+ character password are required.");
        }

        var isHelper = request.AccountType is "DomesticHelp" or "DomesticHelper";
        short accountType = isHelper ? AccountDomesticHelper : AccountUser;

        using var connection = await _db.OpenAsync();

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM users WHERE email = @email)",
            new { email });
        if (exists)
        {
            return (null, "An account with this email already exists.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        using var transaction = connection.BeginTransaction();

        var userId = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO users (full_name, email, password_hash, phone_number, account_type)
            VALUES (@name, @email, @passwordHash, @phone, @accountType)
            RETURNING id
            """,
            new { name, email, passwordHash, phone, accountType },
            transaction);

        await connection.ExecuteAsync(
            "INSERT INTO user_roles (user_id, role_id) VALUES (@userId, @accountType)",
            new { userId, accountType },
            transaction);

        var role = RoleFor(accountType);
        var response = await IssueTokensAsync(connection, transaction, userId, name, email, role, null, ip);

        transaction.Commit();
        return (response, null);
    }

    public async Task<(AuthResponseDto? Data, string? Error)> LoginAsync(LoginRequestDto request, string? ip)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        using var connection = await _db.OpenAsync();

        var user = await connection.QuerySingleOrDefaultAsync<UserRow>(
            """
            SELECT id, full_name AS FullName, email, password_hash AS PasswordHash, account_type AS AccountType
            FROM users
            WHERE email = @email
            """,
            new { email });

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return (null, "Incorrect email or password.");
        }

        var role = RoleFor(user.AccountType);

        using var transaction = connection.BeginTransaction();
        var response = await IssueTokensAsync(connection, transaction, user.Id, user.FullName, user.Email, role, null, ip);
        transaction.Commit();

        return (response, null);
    }

    public async Task<(AuthResponseDto? Data, string? Error)> RefreshAsync(string rawRefreshToken, string? ip)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return (null, "Missing refresh token.");
        }

        byte[] hash;
        try
        {
            hash = JwtTokenService.HashRefreshToken(rawRefreshToken);
        }
        catch
        {
            return (null, "Invalid refresh token.");
        }

        using var connection = await _db.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var token = await connection.QuerySingleOrDefaultAsync<RefreshTokenRow>(
            """
            SELECT id, user_id AS UserId, family_id AS FamilyId,
                   expires_at_utc AS ExpiresAtUtc, revoked_at_utc AS RevokedAtUtc
            FROM refresh_tokens
            WHERE token_hash = @hash
            """,
            new { hash },
            transaction);

        if (token is null)
        {
            return (null, "Invalid refresh token.");
        }

        // The token that a login started with has no family_id; its own id is the family root.
        var familyRoot = token.FamilyId ?? token.Id;

        // A token presented after it was already rotated means someone is replaying it.
        if (token.RevokedAtUtc is not null || token.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await RevokeFamilyAsync(connection, transaction, familyRoot);
            transaction.Commit();
            return (null, "Session expired. Please log in again.");
        }

        var user = await connection.QuerySingleAsync<UserRow>(
            """
            SELECT id, full_name AS FullName, email, password_hash AS PasswordHash, account_type AS AccountType
            FROM users
            WHERE id = @id
            """,
            new { id = token.UserId },
            transaction);

        var role = RoleFor(user.AccountType);

        var (response, newTokenId) = await IssueRefreshTokenAsync(
            connection, transaction, user.Id, user.FullName, user.Email, role, familyRoot, ip);

        await connection.ExecuteAsync(
            "UPDATE refresh_tokens SET revoked_at_utc = now(), replaced_by_token_id = @newTokenId WHERE id = @id",
            new { newTokenId, id = token.Id },
            transaction);

        transaction.Commit();
        return (response, null);
    }

    public async Task LogoutAsync(string rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        byte[] hash;
        try
        {
            hash = JwtTokenService.HashRefreshToken(rawRefreshToken);
        }
        catch
        {
            return;
        }

        using var connection = await _db.OpenAsync();

        var row = await connection.QuerySingleOrDefaultAsync<RefreshTokenRow>(
            "SELECT id, family_id AS FamilyId FROM refresh_tokens WHERE token_hash = @hash",
            new { hash });

        if (row is not null)
        {
            await RevokeFamilyAsync(connection, null, row.FamilyId ?? row.Id);
        }
    }

    private async Task<AuthResponseDto> IssueTokensAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        long userId,
        string name,
        string email,
        string role,
        long? familyRoot,
        string? ip)
    {
        var (response, _) = await IssueRefreshTokenAsync(
            connection, transaction, userId, name, email, role, familyRoot, ip);
        return response;
    }

    private async Task<(AuthResponseDto Response, long NewTokenId)> IssueRefreshTokenAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        long userId,
        string name,
        string email,
        string role,
        long? familyRoot,
        string? ip)
    {
        var (accessToken, expiresAtUtc) = _tokens.CreateAccessToken(userId, name, email, role);
        var (rawRefresh, refreshHash) = JwtTokenService.CreateRefreshToken();

        string? createdByIp = null;
        if (!string.IsNullOrWhiteSpace(ip) && IPAddress.TryParse(ip, out var parsed))
        {
            createdByIp = parsed.ToString();
        }

        var newTokenId = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO refresh_tokens (user_id, token_hash, family_id, expires_at_utc, created_by_ip)
            VALUES (@userId, @refreshHash, @familyRoot, @expiresAt, @createdByIp::inet)
            RETURNING id
            """,
            new
            {
                userId,
                refreshHash,
                familyRoot,
                expiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays),
                createdByIp
            },
            transaction);

        var response = new AuthResponseDto
        {
            Token = accessToken,
            RefreshToken = rawRefresh,
            ExpiresAtUtc = expiresAtUtc,
            UserId = userId.ToString(),
            Name = name,
            Email = email,
            Role = role
        };

        return (response, newTokenId);
    }

    private static string RoleFor(short accountType) => accountType switch
    {
        AccountAdmin => "Admin",
        AccountDomesticHelper => "DomesticHelper",
        _ => "User"
    };

    // Revokes every live token whose family root is the given id (the root itself has family_id IS NULL).
    private static async Task RevokeFamilyAsync(IDbConnection connection, IDbTransaction? transaction, long familyRoot)
    {
        await connection.ExecuteAsync(
            """
            UPDATE refresh_tokens
            SET revoked_at_utc = now()
            WHERE revoked_at_utc IS NULL
              AND (id = @familyRoot OR family_id = @familyRoot)
            """,
            new { familyRoot },
            transaction);
    }

    private sealed class UserRow
    {
        public long Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public short AccountType { get; set; }
    }

    private sealed class RefreshTokenRow
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long? FamilyId { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
    }
}

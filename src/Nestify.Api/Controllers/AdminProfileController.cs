using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Admin;

namespace Nestify.Api.Controllers;

// Reads the admin's own account details from the tables in Autthintication.sql.
[ApiController]
[Route("api/v1/admin/me")]
[Authorize(Roles = "Admin")]
public sealed class AdminProfileController : ControllerBase
{
    private const string Sql = """
        SELECT u.id                                   AS Id,
               u.full_name                            AS FullName,
               u.email                                AS Email,
               u.phone_number                         AS PhoneNumber,
               u.account_type                         AS AccountType,
               u.created_at_utc                       AS CreatedAtUtc,
               r.name                                 AS RoleName,
               r.description                          AS RoleDescription,
               ur.granted_at_utc                      AS RoleGrantedAtUtc,
               (SELECT count(*) FROM refresh_tokens t
                 WHERE t.user_id = u.id
                   AND t.revoked_at_utc IS NULL
                   AND t.expires_at_utc > now())      AS ActiveSessions,
               (SELECT max(t.created_at_utc) FROM refresh_tokens t
                 WHERE t.user_id = u.id)              AS LastSignInUtc,
               (SELECT count(*) FROM users a WHERE a.account_type = 3) AS AdminCount,
               (SELECT count(*) FROM users a WHERE a.account_type <> 3) AS UserCount
          FROM users u
          LEFT JOIN user_roles ur ON ur.user_id = u.id AND ur.role_id = 3
          LEFT JOIN roles r ON r.id = ur.role_id
         WHERE u.id = @id
        """;

    private readonly DbConnectionFactory _db;

    public AdminProfileController(DbConnectionFactory db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<AdminProfileDto>> GetMyProfile()
    {
        using var connection = await _db.OpenAsync();
        var profile = await connection.QuerySingleOrDefaultAsync<AdminProfileDto>(Sql, new { id = RequireUserId() });
        return profile is null ? NotFound() : Ok(profile);
    }

    private long RequireUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}

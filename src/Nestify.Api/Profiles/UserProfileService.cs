using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Profile;

namespace Nestify.Api.Profiles;

public sealed class UserProfileService
{
    private readonly DbConnectionFactory _db;

    public UserProfileService(DbConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Reads the profile of the signed-in user. Accounts registered before this
    /// table existed have no row, so the first read creates one with the default
    /// picture.
    /// </summary>
    public async Task<UserProfileDto?> GetAsync(long userId)
    {
        using var connection = await _db.OpenAsync();

        var profile = await ReadAsync(connection, null, userId);
        if (profile is not null)
        {
            return profile;
        }

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM users WHERE id = @userId)",
            new { userId });
        if (!exists)
        {
            return null;
        }

        await EnsureRowAsync(connection, null, userId);
        return await ReadAsync(connection, null, userId);
    }

    /// <summary>Stores the Cloudinary link of a newly uploaded picture.</summary>
    public async Task<UserProfileDto?> SetPictureAsync(long userId, string pictureUrl)
    {
        using var connection = await _db.OpenAsync();

        await EnsureRowAsync(connection, null, userId);

        await connection.ExecuteAsync(
            """
            UPDATE user_additional_profile_info
            SET profile_picture_url = @pictureUrl,
                updated_at_utc      = now()
            WHERE user_id = @userId
            """,
            new { userId, pictureUrl });

        return await ReadAsync(connection, null, userId);
    }

    public async Task<(UserProfileDto? Data, string? Error)> UpdateAsync(long userId, UpdateUserProfileDto dto)
    {
        using var connection = await _db.OpenAsync();

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM users WHERE id = @userId)",
            new { userId });
        if (!exists)
        {
            return (null, "Account not found.");
        }

        await EnsureRowAsync(connection, null, userId);

        // A null field means "leave it as it is"; an empty box means "clear it".
        await connection.ExecuteAsync(
            """
            UPDATE user_additional_profile_info
            SET occupation          = COALESCE(@occupation, occupation),
                organization_name   = COALESCE(@organizationName, organization_name),
                address             = COALESCE(@address, address),
                whatsapp_number     = COALESCE(@whatsapp, whatsapp_number),
                facebook_url        = COALESCE(@facebook, facebook_url),
                x_url               = COALESCE(@x, x_url),
                instagram_url       = COALESCE(@instagram, instagram_url),
                updated_at_utc      = now()
            WHERE user_id = @userId
            """,
            new
            {
                userId,
                occupation = Clean(dto.Occupation),
                organizationName = Clean(dto.OrganizationName),
                address = Clean(dto.Address),
                whatsapp = Clean(dto.WhatsappNumber),
                facebook = Clean(dto.FacebookUrl),
                x = Clean(dto.XUrl),
                instagram = Clean(dto.InstagramUrl)
            });

        return (await ReadAsync(connection, null, userId), null);
    }

    /// <summary>
    /// Called inside the registration transaction so a new account always has a row.
    /// </summary>
    public static Task EnsureRowAsync(IDbConnection connection, IDbTransaction? transaction, long userId)
        => connection.ExecuteAsync(
            """
            INSERT INTO user_additional_profile_info (user_id, profile_picture_url)
            VALUES (@userId, @defaultPicture)
            ON CONFLICT (user_id) DO NOTHING
            """,
            new { userId, defaultPicture = UserProfileDto.DefaultPictureUrl },
            transaction);

    private static async Task<UserProfileDto?> ReadAsync(IDbConnection connection, IDbTransaction? transaction, long userId)
    {
        var row = await connection.QuerySingleOrDefaultAsync<ProfileRow>(
            """
            SELECT u.id                    AS UserId,
                   u.full_name             AS FullName,
                   u.email                 AS Email,
                   u.phone_number          AS PhoneNumber,
                   u.created_at_utc        AS CreatedAtUtc,
                   p.profile_picture_url   AS ProfilePictureUrl,
                   p.occupation            AS Occupation,
                   p.organization_name     AS OrganizationName,
                   p.is_verified           AS IsVerified,
                   p.address               AS Address,
                   p.whatsapp_number       AS WhatsappNumber,
                   p.facebook_url          AS FacebookUrl,
                   p.x_url                 AS XUrl,
                   p.instagram_url         AS InstagramUrl
            FROM users u
            JOIN user_additional_profile_info p ON p.user_id = u.id
            WHERE u.id = @userId
            """,
            new { userId },
            transaction);

        if (row is null)
        {
            return null;
        }

        return new UserProfileDto
        {
            UserId = row.UserId.ToString(),
            FullName = row.FullName,
            Email = row.Email,
            PhoneNumber = row.PhoneNumber,
            CreatedAtUtc = row.CreatedAtUtc,
            ProfilePictureUrl = string.IsNullOrWhiteSpace(row.ProfilePictureUrl)
                ? UserProfileDto.DefaultPictureUrl
                : row.ProfilePictureUrl,
            Occupation = row.Occupation,
            OrganizationName = row.OrganizationName,
            VerificationState = row.IsVerified
                ? VerificationState.Verified
                : await ReadVerificationStateAsync(connection, userId),
            Address = row.Address,
            WhatsappNumber = row.WhatsappNumber,
            FacebookUrl = row.FacebookUrl,
            XUrl = row.XUrl,
            InstagramUrl = row.InstagramUrl
        };
    }

    private static string? Clean(string? value) => value?.Trim();

    private static async Task<VerificationState> ReadVerificationStateAsync(IDbConnection connection, long userId)
    {
        var status = await connection.ExecuteScalarAsync<short?>(
            "SELECT status FROM verification_requests WHERE user_id = @userId ORDER BY submitted_at_utc DESC LIMIT 1",
            new { userId });
        return status switch
        {
            1 => VerificationState.Pending,
            2 => VerificationState.Verified,
            3 => VerificationState.Rejected,
            4 => VerificationState.NotApplied,
            _ => VerificationState.NotApplied
        };
    }


    private sealed class ProfileRow
    {
        public long UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string ProfilePictureUrl { get; set; } = string.Empty;
        public string? Occupation { get; set; }
        public string? OrganizationName { get; set; }
        public bool IsVerified { get; set; }
        public string? Address { get; set; }
        public string? WhatsappNumber { get; set; }
        public string? FacebookUrl { get; set; }
        public string? XUrl { get; set; }
        public string? InstagramUrl { get; set; }
    }
}

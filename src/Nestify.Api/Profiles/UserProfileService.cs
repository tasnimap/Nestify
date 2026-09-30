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

        var name = Clean(dto.FullName);
        if (dto.FullName is not null && string.IsNullOrWhiteSpace(name))
        {
            return (null, "Full name cannot be empty.");
        }

        var phone = Clean(dto.PhoneNumber);
        if (dto.PhoneNumber is not null && string.IsNullOrWhiteSpace(phone))
        {
            return (null, "Phone number cannot be empty.");
        }

        if (name is not null || phone is not null)
        {
            await connection.ExecuteAsync(
                """
                UPDATE users
                SET full_name    = COALESCE(@name, full_name),
                    phone_number = COALESCE(@phone, phone_number)
                WHERE id = @userId
                """,
                new { userId, name, phone });
        }

        await EnsureRowAsync(connection, null, userId);

        // A null field means "leave it as it is"; an empty box means "clear it".
        await connection.ExecuteAsync(
            """
            UPDATE user_additional_profile_info
            SET occupation          = COALESCE(@occupation, occupation),
                gender              = @gender,
                date_of_birth       = @dateOfBirth,
                is_smoker           = @isSmoker,
                is_drinker          = @isDrinker,
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
                gender = dto.Gender is { } gender ? (short?)gender : null,
                dateOfBirth = dto.DateOfBirth,
                isSmoker = dto.IsSmoker,
                isDrinker = dto.IsDrinker,
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
                   u.account_type          AS AccountType,
                   u.created_at_utc        AS CreatedAtUtc,
                   p.profile_picture_url   AS ProfilePictureUrl,
                   p.occupation            AS Occupation,
                   p.gender                AS Gender,
                   p.date_of_birth         AS DateOfBirth,
                   p.is_smoker             AS IsSmoker,
                   p.is_drinker            AS IsDrinker,
                   p.organization_name     AS OrganizationName,
                   p.is_verified           AS IsVerified,
                   p.address               AS Address,
                   p.whatsapp_number       AS WhatsappNumber,
                   p.facebook_url          AS FacebookUrl,
                   p.x_url                 AS XUrl,
                   p.instagram_url         AS InstagramUrl,
                   hm.role                 AS HomeRole,
                   h.name                  AS HomeName
            FROM users u
            JOIN user_additional_profile_info p ON p.user_id = u.id
            LEFT JOIN home_members hm ON hm.user_id = u.id AND hm.left_at_utc IS NULL
            LEFT JOIN homes h ON h.id = hm.home_id
            WHERE u.id = @userId
            """,
            new { userId },
            transaction);

        if (row is null)
        {
            return null;
        }

        var role = row.AccountType switch
        {
            3 => "Administrator",
            2 => "Domestic Helper",
            _ => row.HomeRole switch
            {
                1 => "Manager",
                2 => "Co-manager",
                3 => "House member",
                _ => "No active home"
            }
        };

        var profile = new UserProfileDto
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
            Gender = row.Gender is { } gender ? (ProfileGender)gender : null,
            DateOfBirth = row.DateOfBirth,
            IsSmoker = row.IsSmoker,
            IsDrinker = row.IsDrinker,
            OrganizationName = row.OrganizationName,
            VerificationState = row.IsVerified
                ? VerificationState.Verified
                : await ReadVerificationStateAsync(connection, userId),
            Address = row.Address,
            WhatsappNumber = row.WhatsappNumber,
            FacebookUrl = row.FacebookUrl,
            XUrl = row.XUrl,
            InstagramUrl = row.InstagramUrl,
            Role = role,
            HomeRole = row.HomeRole,
            HomeName = row.HomeName
        };

        profile.Activity = await ReadActivityAsync(connection, transaction, userId, profile);
        return profile;
    }

    private static string? Clean(string? value) => value?.Trim();

    private static async Task<ProfileActivityDto> ReadActivityAsync(
        IDbConnection connection, IDbTransaction? transaction, long userId, UserProfileDto profile)
    {
        var activity = await connection.QuerySingleAsync<ProfileActivityDto>(
            """
            WITH period AS (
                SELECT EXTRACT(YEAR FROM now() AT TIME ZONE 'Asia/Dhaka')::int AS year,
                       EXTRACT(MONTH FROM now() AT TIME ZONE 'Asia/Dhaka')::int AS month
            ), current_home AS (
                SELECT home_id
                FROM home_members
                WHERE user_id = @userId AND left_at_utc IS NULL
                ORDER BY joined_at_utc DESC, id DESC
                LIMIT 1
            ), current_book AS (
                SELECT r.id, r.house_id
                FROM settlement_runs r
                JOIN current_home h ON h.home_id = r.house_id
                CROSS JOIN period p
                WHERE r.period_year = p.year AND r.period_month = p.month
                LIMIT 1
            ), current_meals AS (
                SELECT DISTINCT ON (m.user_id, m.meal_date)
                       m.user_id, m.meal_date, m.meal_count
                FROM meal_entries m
                JOIN current_book b ON b.house_id = m.house_id
                CROSS JOIN period p
                WHERE m.period_year = p.year AND m.period_month = p.month
                ORDER BY m.user_id, m.meal_date, m.recorded_at_utc DESC, m.id DESC
            ), book_totals AS (
                SELECT (SELECT COUNT(*) FROM settlement_members sm JOIN current_book b ON b.id = sm.settlement_run_id) AS member_count,
                       (SELECT COALESCE(SUM(e.amount), 0) FROM expenses e JOIN current_book b ON b.house_id = e.house_id CROSS JOIN period p WHERE e.period_year = p.year AND e.period_month = p.month AND e.category = 1) AS bills_total,
                       (SELECT COALESCE(SUM(c.amount), 0) FROM contributions c JOIN current_book b ON b.house_id = c.house_id CROSS JOIN period p WHERE c.period_year = p.year AND c.period_month = p.month AND c.fund_type = 1) AS meal_fund,
                       (SELECT COALESCE(SUM(meal_count), 0) FROM current_meals) AS total_meals,
                       (SELECT COALESCE(SUM(meal_count), 0) FROM current_meals WHERE user_id = @userId) AS user_meals
            )
            SELECT
                (SELECT COUNT(*)::int FROM housing_bookings WHERE requester_user_id = @userId AND status IN (1, 2)) AS ActiveBookingCount,
                (SELECT COUNT(*)::int FROM housing_bookings WHERE requester_user_id = @userId AND status = 1) AS PendingBookingCount,
                (SELECT COALESCE(SUM(meal_count), 0) FROM current_meals WHERE user_id = @userId) AS CurrentMonthMeals,
                (SELECT COUNT(*)::int FROM current_meals WHERE user_id = @userId AND meal_count > 0) AS CurrentMonthMealDays,
                EXISTS (SELECT 1 FROM current_book) AS HasCurrentSettlement,
                COALESCE((SELECT SUM(c.amount) FROM contributions c JOIN current_book b ON b.house_id = c.house_id CROSS JOIN period p WHERE c.user_id = @userId AND c.period_year = p.year AND c.period_month = p.month), 0) AS CurrentMonthSettlementPaid,
                CASE WHEN EXISTS (SELECT 1 FROM current_book) THEN
                    ROUND(COALESCE(book_totals.bills_total / NULLIF(book_totals.member_count, 0), 0), 2) +
                    ROUND(COALESCE(book_totals.user_meals * book_totals.meal_fund / NULLIF(book_totals.total_meals, 0), 0), 2)
                ELSE 0 END AS CurrentMonthSettlementDue,
                (SELECT COUNT(*)::int FROM marketplace_buy_interests WHERE buyer_user_id = @userId AND status IN (1, 2)) AS ActiveMarketplaceInterestCount,
                (SELECT COUNT(*)::int FROM marketplace_buy_interests WHERE buyer_user_id = @userId AND status = 1) AS PendingMarketplaceInterestCount
            FROM book_totals
            """, new { userId }, transaction);

        activity.CompletenessPercent = CalculateCompleteness(profile);
        return activity;
    }

    private static int CalculateCompleteness(UserProfileDto profile)
    {
        var fields = new List<bool>
        {
            !string.IsNullOrWhiteSpace(profile.FullName),
            !string.IsNullOrWhiteSpace(profile.Email),
            !string.IsNullOrWhiteSpace(profile.PhoneNumber),
            !string.IsNullOrWhiteSpace(profile.ProfilePictureUrl) && profile.ProfilePictureUrl != UserProfileDto.DefaultPictureUrl,
            !string.IsNullOrWhiteSpace(profile.Occupation),
            profile.Gender is not null,
            profile.DateOfBirth is not null,
            !string.IsNullOrWhiteSpace(profile.Address),
            !string.IsNullOrWhiteSpace(profile.WhatsappNumber),
            !string.IsNullOrWhiteSpace(profile.FacebookUrl) || !string.IsNullOrWhiteSpace(profile.XUrl) || !string.IsNullOrWhiteSpace(profile.InstagramUrl)
        };

        if (profile.Occupation?.StartsWith("Student", StringComparison.Ordinal) == true || profile.Occupation == "Job holder")
        {
            fields.Add(!string.IsNullOrWhiteSpace(profile.OrganizationName));
        }

        return (int)Math.Round(fields.Count(value => value) * 100d / fields.Count, MidpointRounding.AwayFromZero);
    }

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
        public short? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public bool? IsSmoker { get; set; }
        public bool? IsDrinker { get; set; }
        public string? OrganizationName { get; set; }
        public bool IsVerified { get; set; }
        public string? Address { get; set; }
        public string? WhatsappNumber { get; set; }
        public string? FacebookUrl { get; set; }
        public string? XUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public short AccountType { get; set; } = 1;
        public short? HomeRole { get; set; }
        public string? HomeName { get; set; }
    }
}

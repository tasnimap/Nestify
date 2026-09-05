using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Api.Helpers;

public sealed class HelperService
{
    // Mirrors service_engagements.status. DTO's EngagementStatus enum does not
    // number the same way, so we map explicitly rather than casting.
    private const short StatusRequested = 1;
    private const short StatusHelperConfirmed = 2;
    private const short StatusActive = 3;
    private const short StatusCompleted = 4;
    private const short StatusCancelled = 5;
    private const short StatusDeclined = 6;

    private readonly DbConnectionFactory _db;

    public HelperService(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<HelperPageDto<HelperSummaryDto>> BrowseAsync(HelperFilterDto filter)
    {
        using var connection = await _db.OpenAsync();

        var where = new List<string> { "hp.is_active = true" };
        var args = new DynamicParameters();

        if (filter.UpazilaId is not null)
        {
            where.Add("hp.upazila_id = @upazilaId");
            args.Add("upazilaId", filter.UpazilaId);
        }
        else if (filter.DistrictId is not null)
        {
            where.Add("up.district_id = @districtId");
            args.Add("districtId", filter.DistrictId);
        }
        else if (filter.DivisionId is not null)
        {
            where.Add("d.division_id = @divisionId");
            args.Add("divisionId", filter.DivisionId);
        }

        if (filter.ServiceType is not null)
        {
            where.Add("EXISTS (SELECT 1 FROM helper_services hs WHERE hs.helper_profile_id = hp.id AND hs.service_type = @serviceType)");
            args.Add("serviceType", (short)filter.ServiceType.Value);
        }

        if (filter.MaxMonthlyRate is not null)
        {
            where.Add("hp.monthly_rate <= @maxRate");
            args.Add("maxRate", filter.MaxMonthlyRate);
        }

        if (filter.MinRating is not null)
        {
            where.Add("COALESCE(hp.average_rating, 0) >= @minRating");
            args.Add("minRating", filter.MinRating);
        }

        var whereSql = string.Join(" AND ", where);

        var orderSql = filter.Sort switch
        {
            HelperSortOption.RateAsc => "hp.monthly_rate ASC",
            HelperSortOption.RateDesc => "hp.monthly_rate DESC",
            // No stored coordinates for the requesting user yet, so distance sort
            // currently falls back to rate. Revisit once geocoding lands.
            HelperSortOption.DistanceAsc => "hp.monthly_rate ASC",
            _ => "COALESCE(hp.average_rating, 0) DESC"
        };

        var page = Math.Max(filter.Page, 1);
        var pageSize = filter.PageSize <= 0 ? 9 : filter.PageSize;
        var offset = (page - 1) * pageSize;
        args.Add("limit", pageSize);
        args.Add("offset", offset);

        var sql = $"""
            SELECT hp.id, hp.display_name AS Name, hp.monthly_rate AS MonthlyRate,
                   COALESCE(hp.average_rating, 0) AS RatingAverage, hp.review_count AS RatingCount,
                   up.name AS AreaName
            FROM domestic_helper_profiles hp
            JOIN upazilas up ON up.id = hp.upazila_id
            JOIN districts d ON d.id = up.district_id
            WHERE {whereSql}
            ORDER BY {orderSql}
            LIMIT @limit OFFSET @offset
            """;

        var rows = (await connection.QueryAsync<HelperSummaryRow>(sql, args)).ToList();

        var countSql = $"""
            SELECT COUNT(*)
            FROM domestic_helper_profiles hp
            JOIN upazilas up ON up.id = hp.upazila_id
            JOIN districts d ON d.id = up.district_id
            WHERE {whereSql}
            """;
        var total = await connection.ExecuteScalarAsync<int>(countSql, args);

        var servicesByHelper = await LoadServicesAsync(connection, rows.Select(r => r.Id).ToList());

        var items = rows.Select(r => new HelperSummaryDto
        {
            Id = r.Id.ToString(),
            Name = r.Name,
            Services = servicesByHelper.GetValueOrDefault(r.Id, new List<ServiceType>()),
            MonthlyRate = r.MonthlyRate,
            RatingAverage = (double)r.RatingAverage,
            RatingCount = r.RatingCount,
            AreaName = r.AreaName,
            Distance = null
        }).ToList();

        return new HelperPageDto<HelperSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<HelperDetailDto?> GetHelperAsync(string id, long? currentUserId)
    {
        if (!long.TryParse(id, out var helperId))
        {
            return null;
        }

        using var connection = await _db.OpenAsync();

        var row = await connection.QuerySingleOrDefaultAsync<HelperDetailRow>(
            """
            SELECT hp.id, hp.user_id AS UserId, hp.display_name AS Name, hp.monthly_rate AS MonthlyRate,
                   hp.availability_window AS AvailabilityWindow,
                   COALESCE(hp.average_rating, 0) AS RatingAverage, hp.review_count AS RatingCount,
                   up.name AS AreaName
            FROM domestic_helper_profiles hp
            JOIN upazilas up ON up.id = hp.upazila_id
            WHERE hp.id = @helperId AND hp.is_active = true
            """,
            new { helperId });

        if (row is null)
        {
            return null;
        }

        var services = await LoadServicesAsync(connection, new List<long> { row.Id });

        return ToDetailDto(row, services, currentUserId);
    }

    public async Task<HelperDetailDto?> GetMyProfileAsync(long userId)
    {
        using var connection = await _db.OpenAsync();

        var row = await connection.QuerySingleOrDefaultAsync<HelperDetailRow>(
            """
            SELECT hp.id, hp.user_id AS UserId, hp.display_name AS Name, hp.monthly_rate AS MonthlyRate,
                   hp.availability_window AS AvailabilityWindow,
                   COALESCE(hp.average_rating, 0) AS RatingAverage, hp.review_count AS RatingCount,
                   up.name AS AreaName
            FROM domestic_helper_profiles hp
            JOIN upazilas up ON up.id = hp.upazila_id
            WHERE hp.user_id = @userId
            """,
            new { userId });

        if (row is null)
        {
            return null;
        }

        var services = await LoadServicesAsync(connection, new List<long> { row.Id });

        return ToDetailDto(row, services, userId);
    }

    public async Task<(HelperDetailDto? Data, string? Error)> RegisterAsync(long userId, HelperRegistrationDto dto)
    {
        if (dto.UpazilaId is null)
        {
            return (null, "Please select your area.");
        }
        if (dto.Services.Count == 0)
        {
            return (null, "Select at least one service you offer.");
        }
        if (dto.MonthlyRate <= 0)
        {
            return (null, "Enter a monthly rate.");
        }

        using var connection = await _db.OpenAsync();

        var existingId = await connection.ExecuteScalarAsync<long?>(
            "SELECT id FROM domestic_helper_profiles WHERE user_id = @userId",
            new { userId });
        if (existingId is not null)
        {
            return (null, "You already have a helper profile. Use update instead.");
        }

        var fullName = await connection.ExecuteScalarAsync<string>(
            "SELECT full_name FROM users WHERE id = @userId",
            new { userId });

        using var transaction = connection.BeginTransaction();

        var helperId = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO domestic_helper_profiles
                (user_id, display_name, upazila_id, latitude, longitude, monthly_rate, availability_window, years_experience)
            VALUES
                (@userId, @displayName, @upazilaId, NULL, NULL, @monthlyRate, @availabilityWindow, 0)
            RETURNING id
            """,
            new
            {
                userId,
                displayName = fullName,
                upazilaId = dto.UpazilaId,
                monthlyRate = dto.MonthlyRate,
                availabilityWindow = dto.AvailabilityWindow ?? ""
            },
            transaction);

        await ReplaceServicesAsync(connection, transaction, helperId, dto);

        transaction.Commit();

        var profile = await GetMyProfileAsync(userId);
        return (profile, null);
    }

    public async Task<(HelperDetailDto? Data, string? Error)> UpdateProfileAsync(long userId, HelperRegistrationDto dto)
    {
        using var connection = await _db.OpenAsync();

        var helperId = await connection.ExecuteScalarAsync<long?>(
            "SELECT id FROM domestic_helper_profiles WHERE user_id = @userId",
            new { userId });

        if (helperId is null)
        {
            return (null, "No helper profile found. Register first.");
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            UPDATE domestic_helper_profiles
            SET upazila_id = @upazilaId, monthly_rate = @monthlyRate, availability_window = @availabilityWindow
            WHERE id = @helperId
            """,
            new
            {
                helperId,
                upazilaId = dto.UpazilaId,
                monthlyRate = dto.MonthlyRate,
                availabilityWindow = dto.AvailabilityWindow ?? ""
            },
            transaction);

        await connection.ExecuteAsync(
            "DELETE FROM helper_services WHERE helper_profile_id = @helperId",
            new { helperId },
            transaction);

        await ReplaceServicesAsync(connection, transaction, helperId.Value, dto);

        transaction.Commit();

        var profile = await GetMyProfileAsync(userId);
        return (profile, null);
    }

    public async Task<HelperPageDto<ReviewDto>> GetReviewsAsync(string helperId, int page, int pageSize)
    {
        if (!long.TryParse(helperId, out var id))
        {
            return new HelperPageDto<ReviewDto> { Page = page, PageSize = pageSize, TotalCount = 0 };
        }

        using var connection = await _db.OpenAsync();

        var offset = (Math.Max(page, 1) - 1) * pageSize;

        var rows = await connection.QueryAsync<ReviewDto>(
            """
            SELECT u.full_name AS ReviewerName, r.rating AS Rating, r.comment AS Comment, r.created_at_utc AS CreatedAtUtc
            FROM helper_reviews r
            JOIN users u ON u.id = r.reviewer_user_id
            WHERE r.helper_profile_id = @id AND NOT r.is_hidden
            ORDER BY r.created_at_utc DESC
            LIMIT @pageSize OFFSET @offset
            """,
            new { id, pageSize, offset });

        var total = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM helper_reviews WHERE helper_profile_id = @id AND NOT is_hidden",
            new { id });

        return new HelperPageDto<ReviewDto>
        {
            Items = rows.ToList(),
            Page = Math.Max(page, 1),
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<List<EngagementDto>> GetMyEngagementsAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        return await LoadEngagementsForUserAsync(connection, null, userId);
    }

    public async Task<(EngagementDto? Data, string? Error)> RequestEngagementAsync(long clientUserId, string helperId)
    {
        if (!long.TryParse(helperId, out var hpId))
        {
            return (null, "Helper not found.");
        }

        using var connection = await _db.OpenAsync();

        var helperUserId = await connection.ExecuteScalarAsync<long?>(
            "SELECT user_id FROM domestic_helper_profiles WHERE id = @hpId AND is_active = true",
            new { hpId });

        if (helperUserId is null)
        {
            return (null, "Helper not found.");
        }
        if (helperUserId == clientUserId)
        {
            return (null, "You can't request your own profile.");
        }

        using var transaction = connection.BeginTransaction();

        long engagementId;
        try
        {
            engagementId = await connection.ExecuteScalarAsync<long>(
                """
                INSERT INTO service_engagements (helper_profile_id, client_user_id, status, start_date)
                VALUES (@hpId, @clientUserId, @statusRequested, CURRENT_DATE)
                RETURNING id
                """,
                new { hpId, clientUserId, statusRequested = StatusRequested },
                transaction);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            transaction.Rollback();
            return (null, "You already have an open request with this helper.");
        }

        transaction.Commit();

        var engagement = (await LoadEngagementsForUserAsync(connection, null, clientUserId))
            .First(e => e.Id == engagementId.ToString());
        return (engagement, null);
    }

    public async Task<(EngagementDto? Data, string? Error)> ConfirmEngagementAsync(long helperUserId, string engagementId)
    {
        if (!long.TryParse(engagementId, out var id))
        {
            return (null, "Engagement not found.");
        }

        using var connection = await _db.OpenAsync();

        var ownerUserId = await connection.ExecuteScalarAsync<long?>(
            """
            SELECT hp.user_id
            FROM service_engagements e
            JOIN domestic_helper_profiles hp ON hp.id = e.helper_profile_id
            WHERE e.id = @id AND e.status = @statusRequested
            """,
            new { id, statusRequested = StatusRequested });

        if (ownerUserId is null)
        {
            return (null, "Request not found or already handled.");
        }
        if (ownerUserId != helperUserId)
        {
            return (null, "You can only confirm your own requests.");
        }

        await connection.ExecuteAsync(
            "UPDATE service_engagements SET status = @statusConfirmed, helper_confirmed_at_utc = now() WHERE id = @id",
            new { id, statusConfirmed = StatusHelperConfirmed });

        var engagement = (await LoadEngagementsForUserAsync(connection, null, helperUserId))
            .First(e => e.Id == id.ToString());
        return (engagement, null);
    }

    public async Task<(EngagementDto? Data, string? Error)> MarkCompleteAsync(long userId, string engagementId)
    {
        if (!long.TryParse(engagementId, out var id))
        {
            return (null, "Engagement not found.");
        }

        using var connection = await _db.OpenAsync();

        var row = await connection.QuerySingleOrDefaultAsync<EngagementOwnerRow>(
            """
            SELECT e.client_user_id AS ClientUserId, hp.user_id AS HelperUserId, e.status AS Status
            FROM service_engagements e
            JOIN domestic_helper_profiles hp ON hp.id = e.helper_profile_id
            WHERE e.id = @id
            """,
            new { id });

        if (row is null || row.Status != StatusHelperConfirmed)
        {
            return (null, "Engagement isn't active.");
        }

        var isClient = row.ClientUserId == userId;
        var isHelper = row.HelperUserId == userId;
        if (!isClient && !isHelper)
        {
            return (null, "You aren't part of this engagement.");
        }

        using var transaction = connection.BeginTransaction();

        if (isClient)
        {
            await connection.ExecuteAsync(
                "UPDATE service_engagements SET client_completed_at_utc = now() WHERE id = @id",
                new { id }, transaction);
        }
        else
        {
            await connection.ExecuteAsync(
                "UPDATE service_engagements SET helper_completed_at_utc = now() WHERE id = @id",
                new { id }, transaction);
        }

        var bothDone = await connection.ExecuteScalarAsync<bool>(
            "SELECT client_completed_at_utc IS NOT NULL AND helper_completed_at_utc IS NOT NULL FROM service_engagements WHERE id = @id",
            new { id }, transaction);

        if (bothDone)
        {
            await connection.ExecuteAsync(
                "UPDATE service_engagements SET status = @statusCompleted, completed_at_utc = now() WHERE id = @id",
                new { id, statusCompleted = StatusCompleted }, transaction);
        }

        transaction.Commit();

        var engagement = (await LoadEngagementsForUserAsync(connection, null, userId))
            .First(e => e.Id == id.ToString());
        return (engagement, null);
    }

    public async Task<string?> SubmitReviewAsync(long reviewerUserId, string engagementId, int rating, string comment)
    {
        if (rating is < 1 or > 5)
        {
            return "Rating must be between 1 and 5.";
        }
        if (!long.TryParse(engagementId, out var id))
        {
            return "Engagement not found.";
        }

        using var connection = await _db.OpenAsync();

        var row = await connection.QuerySingleOrDefaultAsync<EngagementForReviewRow>(
            "SELECT helper_profile_id AS HelperProfileId, client_user_id AS ClientUserId, status AS Status FROM service_engagements WHERE id = @id",
            new { id });

        if (row is null || row.ClientUserId != reviewerUserId)
        {
            return "You can't review this engagement.";
        }
        if (row.Status != StatusCompleted)
        {
            return "You can only review completed engagements.";
        }

        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO helper_reviews (service_engagement_id, helper_profile_id, reviewer_user_id, rating, comment)
                VALUES (@id, @helperProfileId, @reviewerUserId, @rating, @comment)
                """,
                new { id, helperProfileId = row.HelperProfileId, reviewerUserId, rating, comment },
                transaction);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            transaction.Rollback();
            return "You've already reviewed this engagement.";
        }

        await connection.ExecuteAsync(
            """
            UPDATE domestic_helper_profiles
            SET review_count = review_count + 1,
                average_rating = (COALESCE(average_rating, 0) * review_count + @rating) / (review_count + 1)
            WHERE id = @helperProfileId
            """,
            new { rating, helperProfileId = row.HelperProfileId },
            transaction);

        transaction.Commit();
        return null;
    }

    private static async Task ReplaceServicesAsync(IDbConnection connection, IDbTransaction transaction, long helperId, HelperRegistrationDto dto)
    {
        foreach (var service in dto.Services.Distinct())
        {
            await connection.ExecuteAsync(
                "INSERT INTO helper_services (helper_profile_id, service_type, rate_per_month) VALUES (@helperId, @serviceType, @rate)",
                new { helperId, serviceType = (short)service, rate = dto.MonthlyRate },
                transaction);
        }
    }

    private static async Task<Dictionary<long, List<ServiceType>>> LoadServicesAsync(IDbConnection connection, List<long> helperIds)
    {
        if (helperIds.Count == 0)
        {
            return new Dictionary<long, List<ServiceType>>();
        }

        var rows = await connection.QueryAsync<HelperServiceRow>(
            "SELECT helper_profile_id AS HelperProfileId, service_type AS ServiceType FROM helper_services WHERE helper_profile_id = ANY(@ids)",
            new { ids = helperIds.ToArray() });

        return rows
            .GroupBy(r => r.HelperProfileId)
            .ToDictionary(g => g.Key, g => g.Select(x => (ServiceType)x.ServiceType).ToList());
    }

    private async Task<List<EngagementDto>> LoadEngagementsForUserAsync(IDbConnection connection, IDbTransaction? transaction, long userId)
    {
        var rows = await connection.QueryAsync<EngagementRow>(
            """
            SELECT e.id, e.helper_profile_id AS HelperProfileId, hp.user_id AS HelperUserId,
                   hp.display_name AS HelperName, cu.full_name AS ClientName,
                   e.status AS Status, e.requested_at_utc AS RequestedAtUtc,
                   e.client_completed_at_utc AS ClientCompletedAtUtc,
                   e.helper_completed_at_utc AS HelperCompletedAtUtc,
                   (r.id IS NOT NULL) AS HasReview
            FROM service_engagements e
            JOIN domestic_helper_profiles hp ON hp.id = e.helper_profile_id
            JOIN users cu ON cu.id = e.client_user_id
            LEFT JOIN helper_reviews r ON r.service_engagement_id = e.id
            WHERE e.client_user_id = @userId OR hp.user_id = @userId
            ORDER BY e.requested_at_utc DESC
            """,
            new { userId },
            transaction);

        return rows.Select(r =>
        {
            var isHelper = r.HelperUserId == userId;
            return new EngagementDto
            {
                Id = r.Id.ToString(),
                HelperId = r.HelperProfileId.ToString(),
                HelperName = r.HelperName,
                ClientName = r.ClientName,
                MyRole = isHelper ? EngagementRole.Helper : EngagementRole.Client,
                Status = ToDtoStatus(r.Status),
                CreatedAtUtc = r.RequestedAtUtc,
                ClientMarkedComplete = r.ClientCompletedAtUtc is not null,
                HelperMarkedComplete = r.HelperCompletedAtUtc is not null,
                CanReview = !isHelper && r.Status == StatusCompleted && !r.HasReview
            };
        }).ToList();
    }

    private static HelperDetailDto ToDetailDto(HelperDetailRow row, Dictionary<long, List<ServiceType>> services, long? currentUserId) => new()
    {
        Id = row.Id.ToString(),
        Name = row.Name,
        Services = services.GetValueOrDefault(row.Id, new List<ServiceType>()),
        MonthlyRate = row.MonthlyRate,
        AvailabilityWindow = row.AvailabilityWindow,
        RatingAverage = (double)row.RatingAverage,
        RatingCount = row.RatingCount,
        AreaName = row.AreaName,
        Distance = null,
        IsMine = currentUserId is not null && row.UserId == currentUserId
    };

    // Cancelled has no DTO equivalent yet (EngagementStatus doesn't model it) —
    // it currently surfaces as Completed in the UI. Worth adding a real
    // Cancelled state to the DTO later if that distinction matters.
    private static EngagementStatus ToDtoStatus(short status) => status switch
    {
        StatusRequested => EngagementStatus.Requested,
        StatusDeclined => EngagementStatus.Declined,
        StatusHelperConfirmed => EngagementStatus.HelperConfirmed,
        StatusActive => EngagementStatus.Active,
        StatusCompleted or StatusCancelled => EngagementStatus.Completed,
        _ => EngagementStatus.Requested
    };

    private sealed class HelperSummaryRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyRate { get; set; }
        public decimal RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public string AreaName { get; set; } = string.Empty;
    }

    private sealed class HelperDetailRow
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyRate { get; set; }
        public string AvailabilityWindow { get; set; } = string.Empty;
        public decimal RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public string AreaName { get; set; } = string.Empty;
    }

    private sealed class HelperServiceRow
    {
        public long HelperProfileId { get; set; }
        public short ServiceType { get; set; }
    }

    private sealed class EngagementRow
    {
        public long Id { get; set; }
        public long HelperProfileId { get; set; }
        public long HelperUserId { get; set; }
        public string HelperName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public short Status { get; set; }
        public DateTime RequestedAtUtc { get; set; }
        public DateTime? ClientCompletedAtUtc { get; set; }
        public DateTime? HelperCompletedAtUtc { get; set; }
        public bool HasReview { get; set; }
    }

    private sealed class EngagementOwnerRow
    {
        public long ClientUserId { get; set; }
        public long HelperUserId { get; set; }
        public short Status { get; set; }
    }

    private sealed class EngagementForReviewRow
    {
        public long HelperProfileId { get; set; }
        public long ClientUserId { get; set; }
        public short Status { get; set; }
    }
}
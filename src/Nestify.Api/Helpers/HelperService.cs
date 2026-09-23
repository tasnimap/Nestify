using System.Data;
using Dapper;
using Npgsql;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Api.Helpers;

// The bachelor's side of domestic help (Domestic_Help.sql): browsing helpers,
// reading a profile and its reviews, asking for an engagement and reviewing
// it afterwards. The helper's own workspace is in HelperWorkspaceService.
public sealed class HelperService
{
    private readonly DbConnectionFactory _db;

    public HelperService(DbConnectionFactory db)
    {
        _db = db;
    }

    // ------------------------------------------------------------ browse

    public async Task<HelperPageDto<HelperSummaryDto>> BrowseAsync(HelperFilterDto filter)
    {
        using var connection = await _db.OpenAsync();

        // Paused helpers and profiles still missing services, rate or address stay out.
        var where = new List<string>
        {
            "hp.is_active = true",
            "hp.monthly_rate > 0",
            "EXISTS (SELECT 1 FROM helper_services hs WHERE hs.helper_profile_id = hp.id)",
            "a.helper_profile_id IS NOT NULL"
        };
        var args = new DynamicParameters();

        if (filter.UpazilaId is not null)
        {
            where.Add("a.upazila_id = @upazilaId");
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
            where.Add("coalesce(hp.average_rating, 0) >= @minRating");
            args.Add("minRating", filter.MinRating);
        }

        if (filter.VerifiedOnly)
        {
            where.Add("hp.is_verified");
        }

        var whereSql = string.Join(" AND ", where);

        var orderSql = filter.Sort switch
        {
            HelperSortOption.RateAsc => "hp.monthly_rate ASC, hp.id",
            HelperSortOption.RateDesc => "hp.monthly_rate DESC, hp.id",
            HelperSortOption.ExperienceDesc => "hp.years_experience DESC, hp.id",
            _ => "hp.is_verified DESC, coalesce(hp.average_rating, 0) DESC, hp.review_count DESC, hp.id"
        };

        var page = Math.Max(filter.Page, 1);
        var pageSize = filter.PageSize <= 0 ? 9 : filter.PageSize;
        args.Add("limit", pageSize);
        args.Add("offset", (page - 1) * pageSize);

        var rows = (await connection.QueryAsync<SummaryRow>($"""
            SELECT hp.id, u.full_name AS Name, hp.photo_url AS PhotoUrl, hp.headline AS Headline,
                   hp.monthly_rate AS MonthlyRate, hp.years_experience AS ExperienceYears,
                   coalesce(hp.average_rating, 0) AS RatingAverage, hp.review_count AS RatingCount,
                   coalesce(up.name, '') AS AreaName, hp.is_verified AS IsVerified
            FROM domestic_helper_profiles hp
            JOIN users u ON u.id = hp.user_id
            LEFT JOIN helper_addresses a ON a.helper_profile_id = hp.id
            LEFT JOIN upazilas up ON up.id = a.upazila_id
            LEFT JOIN districts d ON d.id = up.district_id
            WHERE {whereSql}
            ORDER BY {orderSql}
            LIMIT @limit OFFSET @offset
            """, args)).ToList();

        var total = await connection.ExecuteScalarAsync<int>($"""
            SELECT count(*)
            FROM domestic_helper_profiles hp
            LEFT JOIN helper_addresses a ON a.helper_profile_id = hp.id
            LEFT JOIN upazilas up ON up.id = a.upazila_id
            LEFT JOIN districts d ON d.id = up.district_id
            WHERE {whereSql}
            """, args);

        var services = await LoadServicesAsync(connection, rows.Select(r => r.Id).ToList());

        return new HelperPageDto<HelperSummaryDto>
        {
            Items = rows.Select(r => new HelperSummaryDto
            {
                Id = r.Id.ToString(),
                Name = r.Name,
                PhotoUrl = r.PhotoUrl,
                Headline = r.Headline,
                Services = services.GetValueOrDefault(r.Id, new List<ServiceType>()),
                MonthlyRate = r.MonthlyRate,
                ExperienceYears = r.ExperienceYears,
                RatingAverage = (double)r.RatingAverage,
                RatingCount = r.RatingCount,
                AreaName = r.AreaName,
                IsVerified = r.IsVerified
            }).ToList(),
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

        var row = await connection.QuerySingleOrDefaultAsync<DetailRow>("""
            SELECT hp.id, hp.user_id AS UserId, u.full_name AS Name, hp.photo_url AS PhotoUrl,
                   hp.headline AS Headline, coalesce(hp.bio, '') AS Bio, hp.languages AS Languages,
                   hp.monthly_rate AS MonthlyRate, hp.years_experience AS ExperienceYears,
                   coalesce(hp.average_rating, 0) AS RatingAverage, hp.review_count AS RatingCount,
                   coalesce(up.name, '') AS AreaName, coalesce(d.name, '') AS DistrictName,
                   hp.is_verified AS IsVerified, hp.is_active AS IsActive, hp.created_at_utc AS CreatedAtUtc
            FROM domestic_helper_profiles hp
            JOIN users u ON u.id = hp.user_id
            LEFT JOIN helper_addresses a ON a.helper_profile_id = hp.id
            LEFT JOIN upazilas up ON up.id = a.upazila_id
            LEFT JOIN districts d ON d.id = up.district_id
            WHERE hp.id = @helperId
            """, new { helperId });

        if (row is null)
        {
            return null;
        }

        var services = await LoadServicesAsync(connection, new List<long> { row.Id });

        string? activeEngagementId = null;
        bool isWorkingForMyHome = false;
        bool canManageEngagement = false;

        if (currentUserId is not null)
        {
            var activeEng = await connection.QuerySingleOrDefaultAsync<ActiveHomeEngagementRow>("""
                SELECT e.id AS EngagementId,
                       (e.client_user_id = @currentUserId OR EXISTS (
                           SELECT 1 FROM home_members hm
                           WHERE hm.home_id = e.home_id AND hm.user_id = @currentUserId
                             AND hm.left_at_utc IS NULL AND hm.role IN (1, 2)
                       )) AS CanManage
                FROM service_engagements e
                JOIN home_members my_hm ON my_hm.home_id = e.home_id
                     AND my_hm.user_id = @currentUserId
                     AND my_hm.left_at_utc IS NULL
                WHERE e.helper_profile_id = @helperId AND e.status = @activeStatus
                LIMIT 1
                """, new { currentUserId, helperId, activeStatus = (short)EngagementStatus.Active });

            if (activeEng is not null)
            {
                activeEngagementId = activeEng.EngagementId.ToString();
                isWorkingForMyHome = true;
                canManageEngagement = activeEng.CanManage;
            }
        }

        return new HelperDetailDto
        {
            Id = row.Id.ToString(),
            Name = row.Name,
            PhotoUrl = row.PhotoUrl,
            Headline = row.Headline,
            Bio = row.Bio,
            Languages = row.Languages,
            Services = services.GetValueOrDefault(row.Id, new List<ServiceType>()),
            MonthlyRate = row.MonthlyRate,
            ExperienceYears = row.ExperienceYears,
            RatingAverage = (double)row.RatingAverage,
            RatingCount = row.RatingCount,
            AreaName = row.AreaName,
            DistrictName = row.DistrictName,
            IsVerified = row.IsVerified,
            IsAcceptingBookings = row.IsActive,
            MemberSinceUtc = row.CreatedAtUtc,
            Availability = await LoadBoardAsync(connection, row.Id),
            IsMine = currentUserId is not null && row.UserId == currentUserId,
            ActiveEngagementId = activeEngagementId,
            IsWorkingForMyHome = isWorkingForMyHome,
            CanManageEngagement = canManageEngagement
        };
    }

    public async Task<HelperPageDto<ReviewDto>> GetReviewsAsync(string helperId, int page, int pageSize)
    {
        if (!long.TryParse(helperId, out var id))
        {
            return new HelperPageDto<ReviewDto> { Page = page, PageSize = pageSize };
        }

        using var connection = await _db.OpenAsync();
        page = Math.Max(page, 1);
        pageSize = pageSize <= 0 ? 5 : pageSize;

        var rows = (await connection.QueryAsync<ReviewRow>($"""
            {ReviewSql}
            WHERE r.helper_profile_id = @id AND NOT r.is_hidden
            ORDER BY r.created_at_utc DESC
            LIMIT @pageSize OFFSET @offset
            """, new { id, pageSize, offset = (page - 1) * pageSize })).ToList();

        var total = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM helper_reviews WHERE helper_profile_id = @id AND NOT is_hidden", new { id });

        return new HelperPageDto<ReviewDto>
        {
            Items = await ToReviewDtosAsync(connection, rows),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    // ------------------------------------------------------------ engagements (client side)

    public async Task<List<EngagementDto>> GetMyEngagementsAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        return await LoadClientEngagementsAsync(connection, userId, null);
    }

    public async Task<(EngagementDto? Data, string? Error)> RequestEngagementAsync(long clientUserId, string helperId, EngagementRequestDto request)
    {
        if (!long.TryParse(helperId, out var hpId))
        {
            return (null, "Helper not found.");
        }

        var services = request.Services.Distinct().ToList();
        if (services.Count == 0)
        {
            return (null, "Pick at least one service you need.");
        }

        var slots = request.Slots
            .Where(s => s.DayOfWeek is >= 0 and <= 6 && s.Hour is >= 6 and <= 23)
            .DistinctBy(s => (s.DayOfWeek, s.Hour))
            .ToList();
        if (slots.Count == 0)
        {
            return (null, "Pick at least one hour on the board.");
        }

        var message = (request.Message ?? string.Empty).Trim();
        if (message.Length > 1000)
        {
            message = message[..1000];
        }

        using var connection = await _db.OpenAsync();

        var helper = await connection.QuerySingleOrDefaultAsync<HelperTargetRow>(
            "SELECT user_id AS UserId, monthly_rate AS MonthlyRate, is_active AS IsActive FROM domestic_helper_profiles WHERE id = @hpId",
            new { hpId });
        if (helper is null)
        {
            return (null, "Helper not found.");
        }
        if (helper.UserId == clientUserId)
        {
            return (null, "You can't request your own profile.");
        }
        if (!helper.IsActive)
        {
            return (null, "This helper is not taking new bookings right now.");
        }

        // Every picked hour has to be open on her board and not already taken.
        var board = await LoadBoardAsync(connection, hpId);
        foreach (var slot in slots)
        {
            var cell = board.FirstOrDefault(b => b.DayOfWeek == slot.DayOfWeek && b.Hour == slot.Hour);
            if (cell is null || !cell.IsOpen || cell.IsBooked)
            {
                return (null, "One of the hours you picked is no longer available. Refresh the board and try again.");
            }
        }

        // The request is made for a home, so only its manager or a co-manager can send it.
        var homeId = await connection.ExecuteScalarAsync<long?>(
            "SELECT home_id FROM home_members WHERE user_id = @clientUserId AND left_at_utc IS NULL AND role IN (1, 2) LIMIT 1",
            new { clientUserId });
        if (homeId is null)
        {
            return (null, "Only a home's manager or co-manager can book a helper for it. Create a home or ask your manager.");
        }

        using var transaction = connection.BeginTransaction();
        long engagementId;
        try
        {
            engagementId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO service_engagements (helper_profile_id, client_user_id, home_id, monthly_rate, message)
                VALUES (@hpId, @clientUserId, @homeId, @rate, @message)
                RETURNING id
                """, new { hpId, clientUserId, homeId, rate = helper.MonthlyRate, message }, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            transaction.Rollback();
            return (null, "Your home already has an open request with this helper.");
        }

        foreach (var service in services)
        {
            await connection.ExecuteAsync(
                "INSERT INTO service_engagement_services (engagement_id, service_type) VALUES (@engagementId, @type)",
                new { engagementId, type = (short)service }, transaction);
        }

        foreach (var slot in slots)
        {
            await connection.ExecuteAsync(
                "INSERT INTO service_engagement_slots (engagement_id, day_of_week, hour) VALUES (@engagementId, @day, @hour)",
                new { engagementId, day = (short)slot.DayOfWeek, hour = (short)slot.Hour }, transaction);
        }

        transaction.Commit();

        var engagement = (await LoadClientEngagementsAsync(connection, clientUserId, engagementId)).FirstOrDefault();
        return (engagement, null);
    }

    // A client can take back a request the helper has not answered yet.
    public async Task<string?> CancelRequestAsync(long clientUserId, string engagementId)
    {
        if (!long.TryParse(engagementId, out var id))
        {
            return "Request not found.";
        }

        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync("""
            UPDATE service_engagements
            SET status = @cancelled, cancelled_at_utc = now()
            WHERE id = @id AND client_user_id = @clientUserId AND status = @requested
            """, new { id, clientUserId, requested = (short)EngagementStatus.Requested, cancelled = (short)EngagementStatus.Cancelled });

        return changed == 0 ? "This request can't be withdrawn any more." : null;
    }

    // Either side marks the engagement done; it completes once both have.
    public async Task<string?> MarkCompleteAsync(long userId, string engagementId)
    {
        if (!long.TryParse(engagementId, out var id))
        {
            return "Engagement not found.";
        }

        using var connection = await _db.OpenAsync();

        var row = await connection.QuerySingleOrDefaultAsync<OwnerRow>("""
            SELECT e.client_user_id AS ClientUserId, hp.user_id AS HelperUserId, e.status AS Status,
                   EXISTS (SELECT 1 FROM home_members hm WHERE hm.home_id = e.home_id AND hm.user_id = @userId
                           AND hm.left_at_utc IS NULL AND hm.role IN (1, 2)) AS IsHomeManager
            FROM service_engagements e
            JOIN domestic_helper_profiles hp ON hp.id = e.helper_profile_id
            WHERE e.id = @id
            """, new { id, userId });

        if (row is null || row.Status != (short)EngagementStatus.Active)
        {
            return "This engagement isn't active.";
        }

        var isClient = row.ClientUserId == userId || row.IsHomeManager;
        var isHelper = row.HelperUserId == userId;
        if (!isClient && !isHelper)
        {
            return "You aren't part of this engagement.";
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(isClient
                ? "UPDATE service_engagements SET client_completed_at_utc = coalesce(client_completed_at_utc, now()) WHERE id = @id"
                : "UPDATE service_engagements SET helper_completed_at_utc = coalesce(helper_completed_at_utc, now()) WHERE id = @id",
            new { id }, transaction);

        var completed = await connection.ExecuteAsync("""
            UPDATE service_engagements
            SET status = @completed, completed_at_utc = now()
            WHERE id = @id AND status = @active AND client_completed_at_utc IS NOT NULL AND helper_completed_at_utc IS NOT NULL
            """, new { id, completed = (short)EngagementStatus.Completed, active = (short)EngagementStatus.Active }, transaction);

        // The day both sides call it done is the day she left the home.
        if (completed > 0)
        {
            await connection.ExecuteAsync(
                "UPDATE helper_home_placements SET left_on = CURRENT_DATE WHERE engagement_id = @id AND left_on IS NULL",
                new { id }, transaction);
        }

        transaction.Commit();
        return null;
    }

    // A home manager or co-manager releases the helper from their home, immediately concluding the engagement.
    public async Task<string?> ReleaseEngagementAsync(long clientUserId, string engagementId)
    {
        if (!long.TryParse(engagementId, out var id))
        {
            return "Engagement not found.";
        }

        using var connection = await _db.OpenAsync();

        var row = await connection.QuerySingleOrDefaultAsync<OwnerRow>("""
            SELECT e.client_user_id AS ClientUserId, hp.user_id AS HelperUserId, e.status AS Status,
                   EXISTS (SELECT 1 FROM home_members hm WHERE hm.home_id = e.home_id AND hm.user_id = @clientUserId
                           AND hm.left_at_utc IS NULL AND hm.role IN (1, 2)) AS IsHomeManager
            FROM service_engagements e
            JOIN domestic_helper_profiles hp ON hp.id = e.helper_profile_id
            WHERE e.id = @id
            """, new { id, clientUserId });

        if (row is null || row.Status != (short)EngagementStatus.Active)
        {
            return "This engagement isn't active.";
        }

        var isClient = row.ClientUserId == clientUserId || row.IsHomeManager;
        if (!isClient)
        {
            return "Only a manager or co-manager of the home can release the helper.";
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync("""
            UPDATE service_engagements
            SET status = @completed,
                client_completed_at_utc = coalesce(client_completed_at_utc, now()),
                completed_at_utc = now()
            WHERE id = @id AND status = @active
            """, new { id, completed = (short)EngagementStatus.Completed, active = (short)EngagementStatus.Active }, transaction);

        await connection.ExecuteAsync("""
            UPDATE helper_home_placements
            SET left_on = CURRENT_DATE
            WHERE engagement_id = @id AND left_on IS NULL
            """, new { id }, transaction);

        transaction.Commit();
        return null;
    }

    // Anyone who lived in the home while the helper worked there took her
    // service, so any of them can review that placement, once each.
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

        comment = (comment ?? string.Empty).Trim();
        if (comment.Length > 1000)
        {
            comment = comment[..1000];
        }

        using var connection = await _db.OpenAsync();

        var placement = await connection.QuerySingleOrDefaultAsync<PlacementRow>("""
            SELECT p.id AS PlacementId, p.helper_profile_id AS HelperProfileId
            FROM helper_home_placements p
            WHERE p.engagement_id = @id
              AND EXISTS (SELECT 1 FROM home_members hm
                          WHERE hm.home_id = p.home_id AND hm.user_id = @reviewerUserId
                            AND hm.joined_at_utc::date <= coalesce(p.left_on, CURRENT_DATE)
                            AND (hm.left_at_utc IS NULL OR hm.left_at_utc::date >= p.joined_on))
            """, new { id, reviewerUserId });

        if (placement is null)
        {
            return "Only someone who lived in the home while she worked there can review her.";
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync("""
                INSERT INTO helper_reviews (placement_id, helper_profile_id, reviewer_user_id, rating, comment)
                VALUES (@placementId, @helperProfileId, @reviewerUserId, @rating, @comment)
                """, new { placement.PlacementId, placement.HelperProfileId, reviewerUserId, rating, comment }, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            transaction.Rollback();
            return "You've already reviewed her for this engagement.";
        }

        await connection.ExecuteAsync("""
            UPDATE domestic_helper_profiles hp
            SET average_rating = s.avg_rating, review_count = s.total
            FROM (SELECT avg(rating) AS avg_rating, count(*)::int AS total FROM helper_reviews WHERE helper_profile_id = @helperProfileId AND NOT is_hidden) s
            WHERE hp.id = @helperProfileId
            """, new { placement.HelperProfileId }, transaction);

        transaction.Commit();
        return null;
    }

    // ------------------------------------------------------------ shared lookups

    internal static async Task<Dictionary<long, List<ServiceType>>> LoadServicesAsync(IDbConnection connection, List<long> helperIds)
    {
        if (helperIds.Count == 0)
        {
            return new Dictionary<long, List<ServiceType>>();
        }

        var rows = await connection.QueryAsync<ServiceRow>(
            "SELECT helper_profile_id AS OwnerId, service_type AS ServiceType FROM helper_services WHERE helper_profile_id = ANY(@ids) ORDER BY service_type",
            new { ids = helperIds.ToArray() });

        return rows.GroupBy(r => r.OwnerId).ToDictionary(g => g.Key, g => g.Select(x => (ServiceType)x.ServiceType).ToList());
    }

    internal static async Task<Dictionary<long, List<ServiceType>>> LoadEngagementServicesAsync(IDbConnection connection, List<long> engagementIds)
    {
        if (engagementIds.Count == 0)
        {
            return new Dictionary<long, List<ServiceType>>();
        }

        var rows = await connection.QueryAsync<ServiceRow>(
            "SELECT engagement_id AS OwnerId, service_type AS ServiceType FROM service_engagement_services WHERE engagement_id = ANY(@ids) ORDER BY service_type",
            new { ids = engagementIds.ToArray() });

        return rows.GroupBy(r => r.OwnerId).ToDictionary(g => g.Key, g => g.Select(x => (ServiceType)x.ServiceType).ToList());
    }

    internal static async Task<Dictionary<long, List<EngagementSlotDto>>> LoadSlotsAsync(IDbConnection connection, List<long> engagementIds)
    {
        if (engagementIds.Count == 0)
        {
            return new Dictionary<long, List<EngagementSlotDto>>();
        }

        var rows = await connection.QueryAsync<SlotRow>(
            "SELECT engagement_id AS EngagementId, day_of_week AS DayOfWeek, hour AS Hour FROM service_engagement_slots WHERE engagement_id = ANY(@ids) ORDER BY day_of_week, hour",
            new { ids = engagementIds.ToArray() });

        return rows.GroupBy(r => r.EngagementId)
            .ToDictionary(g => g.Key, g => g.Select(x => new EngagementSlotDto { DayOfWeek = x.DayOfWeek, Hour = x.Hour }).ToList());
    }

    // The full 7 x 18 board: open hours from the weekly template, booked hours
    // from every active engagement.
    internal static async Task<List<HelperAvailabilitySlotDto>> LoadBoardAsync(IDbConnection connection, long helperId)
    {
        var open = (await connection.QueryAsync<(int DayOfWeek, int Hour)>(
            "SELECT day_of_week::int, hour::int FROM helper_weekly_availability WHERE helper_profile_id = @helperId",
            new { helperId })).ToHashSet();

        var booked = (await connection.QueryAsync<(int DayOfWeek, int Hour)>("""
            SELECT s.day_of_week::int, s.hour::int
            FROM service_engagement_slots s
            JOIN service_engagements e ON e.id = s.engagement_id
            WHERE e.helper_profile_id = @helperId AND e.status = @active
            """, new { helperId, active = (short)EngagementStatus.Active })).ToHashSet();

        var board = new List<HelperAvailabilitySlotDto>(7 * 18);
        for (var day = 0; day < 7; day++)
        {
            for (var hour = 6; hour < 24; hour++)
            {
                board.Add(new HelperAvailabilitySlotDto
                {
                    DayOfWeek = day,
                    Hour = hour,
                    IsOpen = open.Contains((day, hour)),
                    IsBooked = booked.Contains((day, hour))
                });
            }
        }
        return board;
    }

    // Shared between the public profile and the helper's own reviews page.
    internal const string ReviewSql = """
        SELECT r.id, pl.engagement_id AS EngagementId, u.full_name AS ReviewerName,
               coalesce(p.profile_picture_url, '') AS ReviewerPhotoUrl, h.name AS HomeName,
               r.rating, r.comment, r.created_at_utc AS CreatedAtUtc, r.reply, r.replied_at_utc AS RepliedAtUtc
        FROM helper_reviews r
        JOIN helper_home_placements pl ON pl.id = r.placement_id
        JOIN homes h ON h.id = pl.home_id
        JOIN users u ON u.id = r.reviewer_user_id
        LEFT JOIN user_additional_profile_info p ON p.user_id = u.id
        """;

    internal static async Task<List<ReviewDto>> ToReviewDtosAsync(IDbConnection connection, List<ReviewRow> rows)
    {
        var services = await LoadEngagementServicesAsync(connection, rows.Select(r => r.EngagementId).ToList());
        return rows.Select(r => new ReviewDto
        {
            Id = r.Id.ToString(),
            ReviewerName = r.ReviewerName,
            ReviewerPhotoUrl = r.ReviewerPhotoUrl,
            HomeName = r.HomeName,
            Services = services.GetValueOrDefault(r.EngagementId, new List<ServiceType>()).Select(ServiceTypes.Label).ToList(),
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAtUtc = r.CreatedAtUtc,
            Reply = r.Reply,
            RepliedAtUtc = r.RepliedAtUtc
        }).ToList();
    }

    private static async Task<List<EngagementDto>> LoadClientEngagementsAsync(IDbConnection connection, long clientUserId, long? onlyId)
    {
        var rows = (await connection.QueryAsync<ClientEngagementRow>("""
            SELECT e.id, e.helper_profile_id AS HelperProfileId, u.full_name AS HelperName, hp.photo_url AS HelperPhotoUrl,
                   u.phone_number AS HelperPhone, coalesce(up.name, '') AS HelperArea,
                   h.name AS HomeName, cu.full_name AS RequesterName, e.client_user_id = @clientUserId AS IsRequester,
                   e.monthly_rate AS MonthlyRate, e.message, e.status, e.requested_at_utc AS RequestedAtUtc,
                   e.start_date AS StartDate, e.decline_reason AS DeclineReason,
                   pl.joined_on AS JoinedOn, pl.left_on AS LeftOn,
                   e.client_completed_at_utc IS NOT NULL AS ClientMarkedComplete,
                   e.helper_completed_at_utc IS NOT NULL AS HelperMarkedComplete,
                   EXISTS (SELECT 1 FROM home_members hm WHERE hm.home_id = e.home_id AND hm.user_id = @clientUserId
                           AND hm.left_at_utc IS NULL AND hm.role IN (1, 2)) AS IsHomeManager,
                   pl.id IS NOT NULL AND EXISTS (SELECT 1 FROM home_members hm
                           WHERE hm.home_id = e.home_id AND hm.user_id = @clientUserId
                             AND hm.joined_at_utc::date <= coalesce(pl.left_on, CURRENT_DATE)
                             AND (hm.left_at_utc IS NULL OR hm.left_at_utc::date >= pl.joined_on)) AS LivedThere,
                   EXISTS (SELECT 1 FROM helper_reviews r WHERE r.placement_id = pl.id AND r.reviewer_user_id = @clientUserId) AS HasReview
            FROM service_engagements e
            JOIN domestic_helper_profiles hp ON hp.id = e.helper_profile_id
            JOIN users u ON u.id = hp.user_id
            JOIN users cu ON cu.id = e.client_user_id
            JOIN homes h ON h.id = e.home_id
            LEFT JOIN helper_addresses a ON a.helper_profile_id = hp.id
            LEFT JOIN upazilas up ON up.id = a.upazila_id
            LEFT JOIN helper_home_placements pl ON pl.engagement_id = e.id
            WHERE (@onlyId IS NULL OR e.id = @onlyId)
              AND (e.client_user_id = @clientUserId
                   OR (pl.id IS NOT NULL AND EXISTS (SELECT 1 FROM home_members hm
                           WHERE hm.home_id = e.home_id AND hm.user_id = @clientUserId
                             AND hm.joined_at_utc::date <= coalesce(pl.left_on, CURRENT_DATE)
                             AND (hm.left_at_utc IS NULL OR hm.left_at_utc::date >= pl.joined_on))))
            ORDER BY e.requested_at_utc DESC
            """, new { clientUserId, onlyId })).ToList();

        var ids = rows.Select(r => r.Id).ToList();
        var services = await LoadEngagementServicesAsync(connection, ids);
        var slots = await LoadSlotsAsync(connection, ids);

        return rows.Select(r =>
        {
            var status = (EngagementStatus)r.Status;
            return new EngagementDto
            {
                Id = r.Id.ToString(),
                HelperId = r.HelperProfileId.ToString(),
                HelperName = r.HelperName,
                HelperPhotoUrl = r.HelperPhotoUrl,
                HelperPhone = status is EngagementStatus.Active or EngagementStatus.Completed ? r.HelperPhone : null,
                HelperArea = r.HelperArea,
                HomeName = r.HomeName,
                RequesterName = r.RequesterName,
                IsRequester = r.IsRequester,
                JoinedOn = r.JoinedOn?.ToDateTime(TimeOnly.MinValue),
                LeftOn = r.LeftOn?.ToDateTime(TimeOnly.MinValue),
                Services = services.GetValueOrDefault(r.Id, new List<ServiceType>()),
                MonthlyRate = r.MonthlyRate,
                Message = r.Message,
                Slots = slots.GetValueOrDefault(r.Id, new List<EngagementSlotDto>()),
                Status = status,
                RequestedAtUtc = r.RequestedAtUtc,
                StartDate = r.StartDate?.ToDateTime(TimeOnly.MinValue),
                DeclineReason = r.DeclineReason,
                ClientMarkedComplete = r.ClientMarkedComplete,
                HelperMarkedComplete = r.HelperMarkedComplete,
                CanManage = r.IsRequester || r.IsHomeManager,
                HasReview = r.HasReview,
                CanReview = r.LivedThere && !r.HasReview
            };
        }).ToList();
    }

    // ------------------------------------------------------------ rows

    private sealed class SummaryRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
        public string Headline { get; set; } = string.Empty;
        public decimal MonthlyRate { get; set; }
        public int ExperienceYears { get; set; }
        public decimal RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }

    private sealed class DetailRow
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
        public string Headline { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Languages { get; set; } = string.Empty;
        public decimal MonthlyRate { get; set; }
        public int ExperienceYears { get; set; }
        public decimal RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string DistrictName { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    internal sealed class ReviewRow
    {
        public long Id { get; set; }
        public long EngagementId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerPhotoUrl { get; set; } = string.Empty;
        public string HomeName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string? Reply { get; set; }
        public DateTime? RepliedAtUtc { get; set; }
    }

    private sealed class ServiceRow
    {
        public long OwnerId { get; set; }
        public short ServiceType { get; set; }
    }

    private sealed class SlotRow
    {
        public long EngagementId { get; set; }
        public int DayOfWeek { get; set; }
        public int Hour { get; set; }
    }

    private sealed class HelperTargetRow
    {
        public long UserId { get; set; }
        public decimal MonthlyRate { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class ActiveHomeEngagementRow
    {
        public long EngagementId { get; set; }
        public bool CanManage { get; set; }
    }

    private sealed class OwnerRow
    {
        public long ClientUserId { get; set; }
        public long HelperUserId { get; set; }
        public short Status { get; set; }
        public bool IsHomeManager { get; set; }
    }

    private sealed class PlacementRow
    {
        public long PlacementId { get; set; }
        public long HelperProfileId { get; set; }
    }

    private sealed class ClientEngagementRow
    {
        public long Id { get; set; }
        public long HelperProfileId { get; set; }
        public string HelperName { get; set; } = string.Empty;
        public string HelperPhotoUrl { get; set; } = string.Empty;
        public string HelperPhone { get; set; } = string.Empty;
        public string HelperArea { get; set; } = string.Empty;
        public string HomeName { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public bool IsRequester { get; set; }
        public decimal MonthlyRate { get; set; }
        public string Message { get; set; } = string.Empty;
        public short Status { get; set; }
        public DateTime RequestedAtUtc { get; set; }
        public DateOnly? StartDate { get; set; }
        public string? DeclineReason { get; set; }
        public DateOnly? JoinedOn { get; set; }
        public DateOnly? LeftOn { get; set; }
        public bool ClientMarkedComplete { get; set; }
        public bool HelperMarkedComplete { get; set; }
        public bool IsHomeManager { get; set; }
        public bool LivedThere { get; set; }
        public bool HasReview { get; set; }
    }
}

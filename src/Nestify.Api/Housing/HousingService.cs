using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Api.Homes;
using Nestify.Api.Profiles;
using Nestify.Shared.Dtos.Housing;

namespace Nestify.Api.Housing;

// Everything the housing pages do, against the tables in Housing.sql. A post
// belongs to a home; whoever is manager or co-manager of that home owns the
// post. Seats available are never stored - they are max_occupants minus the
// people living there, read fresh every time.
public sealed class HousingService
{
    private const short PostActive = 1;
    private const short PostClosed = 2;
    private const short PostFilled = 3;

    private const short BookingPending = 1;
    private const short BookingAccepted = 2;
    private const short BookingRejected = 3;
    private const short BookingWithdrawn = 4;

    private const int MaxImages = 6;

    // Columns every post read shares. Seats are worked out here from the home.
    private const string PostSelect = @"
        SELECT p.id AS Id, p.home_id AS HomeId, p.posted_by_user_id AS PostedByUserId,
               p.title AS Title, p.description AS Description, p.listing_type_id AS ListingTypeId,
               p.monthly_rent_bdt AS MonthlyRent, p.status AS Status, p.created_at_utc AS CreatedAtUtc,
               h.area_name AS AreaName, h.division AS Division,
               GREATEST(0, COALESCE(c.max_occupants, 4) -
                   (SELECT count(*) FROM home_members m WHERE m.home_id = h.id AND m.left_at_utc IS NULL) -
                   (SELECT count(*)
                      FROM housing_bookings b
                      JOIN housing_posts reserved_post ON reserved_post.id = b.post_id
                     WHERE reserved_post.home_id = h.id AND b.status = 2)) AS SeatsAvailable,
               r.gender AS Gender, r.occupation AS Occupation, r.min_age AS MinAge, r.max_age AS MaxAge,
               COALESCE(r.verified_only, false) AS VerifiedOnly,
               COALESCE(r.non_smoker_only, false) AS NonSmokerOnly,
               COALESCE(r.non_drinker_only, false) AS NonDrinkerOnly
        FROM housing_posts p
        JOIN homes h ON h.id = p.home_id
        LEFT JOIN home_capacity c ON c.home_id = h.id
        LEFT JOIN housing_post_requirements r ON r.post_id = p.id";

    private readonly DbConnectionFactory _db;
    private readonly CloudinaryUploader _uploader;

    public HousingService(DbConnectionFactory db, CloudinaryUploader uploader)
    {
        _db = db;
        _uploader = uploader;
    }

    // ------------------------------------------------------------ houses

    // The home the caller can post under: the one they manage or co-manage,
    // with the numbers the form shows next to it.
    public async Task<List<HouseOptionDto>> GetManageableHousesAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<HouseOptionDto>(
            @"SELECT h.id::text AS Id, h.name AS Name, h.area_name AS AreaName, h.division AS Division,
                     COALESCE(c.max_occupants, 4) AS MaxOccupants,
                     (SELECT count(*) FROM home_members m WHERE m.home_id = h.id AND m.left_at_utc IS NULL) AS CurrentOccupants
              FROM home_members hm
              JOIN homes h ON h.id = hm.home_id
              LEFT JOIN home_capacity c ON c.home_id = h.id
              WHERE hm.user_id = @userId AND hm.left_at_utc IS NULL AND hm.role IN (@manager, @coManager)",
            new { userId, manager = HomeService.RoleManager, coManager = HomeService.RoleCoManager });
        return rows.ToList();
    }

    // ------------------------------------------------------------ browse

    // Posts from the caller's own home never show in browse; those live on "my posts".
    // "Verified accounts only" is the one requirement the database can check today,
    // so an unverified seeker does not see those posts at all.
    public async Task<HousingPageDto<HousingPostSummaryDto>> BrowseAsync(long userId, HousingPostFilterDto filter)
    {
        using var connection = await _db.OpenAsync();

        var where = new List<string>
        {
            "p.status = @active",
            "NOT EXISTS (SELECT 1 FROM post_takedowns t WHERE t.scope = 1 AND t.post_id = p.id AND t.restored_at_utc IS NULL)",
            "p.home_id <> COALESCE((SELECT home_id FROM home_members WHERE user_id = @userId AND left_at_utc IS NULL), 0)",
            "(COALESCE(r.verified_only, false) = false OR COALESCE((SELECT is_verified FROM user_additional_profile_info WHERE user_id = @userId), false))",
            @"GREATEST(0, COALESCE(c.max_occupants, 4)
                 - (SELECT count(*) FROM home_members m WHERE m.home_id = h.id AND m.left_at_utc IS NULL)
                 - (SELECT count(*) FROM housing_bookings b JOIN housing_posts reserved_post ON reserved_post.id = b.post_id WHERE reserved_post.home_id = h.id AND b.status = @accepted)) > 0",
            PersonalRequirementsMatchSql
        };
        var args = new DynamicParameters();
        args.Add("active", PostActive);
        args.Add("accepted", BookingAccepted);
        args.Add("userId", userId);

        if (filter.ListingType is { } type)
        {
            where.Add("p.listing_type_id = @type");
            args.Add("type", (short)type);
        }

        if (filter.MaxRent is { } maxRent)
        {
            where.Add("p.monthly_rent_bdt <= @maxRent");
            args.Add("maxRent", maxRent);
        }

        // Homes carry names, the filter carries ids, so the ids are turned into
        // names against the administrative tables.
        if (filter.UpazilaId is { } upazilaId)
        {
            where.Add("lower(h.area_name) = (SELECT lower(name) FROM upazilas WHERE id = @upazilaId)");
            args.Add("upazilaId", upazilaId);
        }
        else if (filter.DistrictId is { } districtId)
        {
            where.Add("lower(h.area_name) IN (SELECT lower(name) FROM upazilas WHERE district_id = @districtId)");
            args.Add("districtId", districtId);
        }
        else if (filter.DivisionId is { } divisionId)
        {
            where.Add("lower(h.division) = (SELECT lower(name) FROM divisions WHERE id = @divisionId)");
            args.Add("divisionId", divisionId);
        }

        var whereSql = string.Join(" AND ", where);
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);
        args.Add("offset", (page - 1) * pageSize);
        args.Add("limit", pageSize);

        var total = await connection.ExecuteScalarAsync<int>(
            $@"SELECT count(*)
               FROM housing_posts p
               JOIN homes h ON h.id = p.home_id
               LEFT JOIN home_capacity c ON c.home_id = h.id
               LEFT JOIN housing_post_requirements r ON r.post_id = p.id
               WHERE {whereSql}",
            args);

        var rows = (await connection.QueryAsync<PostRow>(
            $"{PostSelect} WHERE {whereSql} ORDER BY p.created_at_utc DESC OFFSET @offset LIMIT @limit",
            args)).ToList();

        var images = await LoadImagesAsync(connection, rows.Select(r => r.Id));

        return new HousingPageDto<HousingPostSummaryDto>
        {
            Items = rows.Select(r => ToSummary(r, images)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    // Null for a missing post and for one the caller may not see, on purpose.
    public async Task<HousingPostDetailDto?> GetPostAsync(long userId, long postId)
    {
        using var connection = await _db.OpenAsync();
        var row = await LoadPostAsync(connection, postId);
        if (row is null)
        {
            return null;
        }

        var isMine = await IsOwnerAsync(connection, userId, row.HomeId);
        if (!isMine)
        {
            var hasBooking = await HasBookingAsync(connection, userId, postId);
            // Somebody who asked for a seat can still open the post after it
            // closed or filled, so their booking row has something to link to.
            if (row.Status != PostActive && !hasBooking)
            {
                return null;
            }

            if (row.VerifiedOnly && !await IsVerifiedAsync(connection, userId))
            {
                return null;
            }

            if (!hasBooking && !await MatchesPersonalRequirementsAsync(connection, userId, postId))
            {
                return null;
            }

            // A post an admin struck down is gone for everyone but the owner.
            var takenDown = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM post_takedowns WHERE scope = 1 AND post_id = @postId AND restored_at_utc IS NULL)",
                new { postId });
            if (takenDown)
            {
                return null;
            }
        }

        var images = await LoadImagesAsync(connection, new[] { row.Id });
        var detail = ToDetail(row, images, isMine);
        detail.ViewerHasHome = await HasHomeAsync(connection, userId);
        return detail;
    }

    // ------------------------------------------------------------ create + edit + mine

    public async Task<(long? Id, string? Error)> CreateAsync(long userId, CreateHousingPostRequestDto dto)
    {
        if (!long.TryParse(dto.HouseId, out var homeId))
        {
            return (null, "Pick which house this listing belongs to.");
        }

        var error = Validate(dto.Title, dto.Description, dto.MonthlyRent, dto.Eligibility);
        if (error is not null)
        {
            return (null, error);
        }

        using var connection = await _db.OpenAsync();
        if (!await IsOwnerAsync(connection, userId, homeId))
        {
            return (null, "Only the manager or a co-manager can post for this house.");
        }

        if (await FreeSeatsAsync(connection, homeId) == 0)
        {
            return (null, "This house is full - there is no seat to post about.");
        }

        var (urls, uploadError) = await StoreImagesAsync(dto.ImageUrls);
        if (uploadError is not null)
        {
            return (null, uploadError);
        }

        using var transaction = connection.BeginTransaction();
        var postId = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO housing_posts (home_id, posted_by_user_id, title, description, listing_type_id, monthly_rent_bdt)
              VALUES (@homeId, @userId, @title, @description, @type, @rent)
              RETURNING id",
            new
            {
                homeId,
                userId,
                title = dto.Title.Trim(),
                description = dto.Description.Trim(),
                type = (short)dto.ListingType,
                rent = dto.MonthlyRent
            },
            transaction);

        await SaveRequirementsAsync(connection, transaction, postId, dto.Eligibility);
        await SaveImagesAsync(connection, transaction, postId, urls);
        transaction.Commit();

        return (postId, null);
    }

    public async Task<HousingPostDetailDto?> GetPostForEditAsync(long userId, long postId)
    {
        using var connection = await _db.OpenAsync();
        var row = await LoadPostAsync(connection, postId);
        if (row is null || !await IsOwnerAsync(connection, userId, row.HomeId))
        {
            return null;
        }

        var images = await LoadImagesAsync(connection, new[] { row.Id });
        return ToDetail(row, images, isMine: true);
    }

    public async Task<(bool Ok, string Message)> UpdateAsync(long userId, long postId, UpdateHousingPostRequestDto dto)
    {
        var error = Validate(dto.Title, dto.Description, dto.MonthlyRent, dto.Eligibility);
        if (error is not null)
        {
            return (false, error);
        }

        using var connection = await _db.OpenAsync();
        var row = await LoadPostAsync(connection, postId);
        if (row is null || !await IsOwnerAsync(connection, userId, row.HomeId))
        {
            return (false, "Post not found.");
        }

        var (urls, uploadError) = await StoreImagesAsync(dto.ImageUrls);
        if (uploadError is not null)
        {
            return (false, uploadError);
        }

        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(
            @"UPDATE housing_posts
              SET title = @title, description = @description, listing_type_id = @type,
                  monthly_rent_bdt = @rent, updated_at_utc = now()
              WHERE id = @postId",
            new
            {
                title = dto.Title.Trim(),
                description = dto.Description.Trim(),
                type = (short)dto.ListingType,
                rent = dto.MonthlyRent,
                postId
            },
            transaction);

        await connection.ExecuteAsync(
            "DELETE FROM housing_post_requirements WHERE post_id = @postId", new { postId }, transaction);
        await SaveRequirementsAsync(connection, transaction, postId, dto.Eligibility);

        await connection.ExecuteAsync(
            "DELETE FROM housing_post_images WHERE post_id = @postId", new { postId }, transaction);
        await SaveImagesAsync(connection, transaction, postId, urls);
        transaction.Commit();

        return (true, "Post updated.");
    }

    // Every post of the home the caller manages, closed ones included.
    public async Task<List<MyHousingPostDto>> GetMineAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var rows = (await connection.QueryAsync<PostRow>(
            $@"{PostSelect}
               JOIN home_members hm ON hm.home_id = p.home_id AND hm.user_id = @userId
                                    AND hm.left_at_utc IS NULL AND hm.role IN (@manager, @coManager)
               ORDER BY p.created_at_utc DESC",
            new { userId, manager = HomeService.RoleManager, coManager = HomeService.RoleCoManager })).ToList();

        var ids = rows.Select(r => r.Id).ToArray();
        var images = await LoadImagesAsync(connection, ids);

        var counts = (await connection.QueryAsync<CountRow>(
            @"SELECT post_id AS PostId,
                     count(*) AS Total,
                     count(*) FILTER (WHERE status = @pending) AS Pending
              FROM housing_bookings
              WHERE post_id = ANY(@ids)
              GROUP BY post_id",
            new { ids, pending = BookingPending })).ToDictionary(c => c.PostId);

        return rows.Select(r => new MyHousingPostDto
        {
            Post = ToSummary(r, images),
            BookingRequestCount = counts.TryGetValue(r.Id, out var c) ? c.Total : 0,
            PendingBookingRequestCount = counts.TryGetValue(r.Id, out var p) ? p.Pending : 0
        }).ToList();
    }

    public Task<bool> CloseAsync(long userId, long postId) =>
        SetStatusAsync(userId, postId, PostClosed);

    public async Task<bool> ReopenAsync(long userId, long postId)
    {
        using var connection = await _db.OpenAsync();
        var row = await LoadPostAsync(connection, postId);
        if (row is null || await FreeSeatsAsync(connection, row.HomeId) == 0)
        {
            return false;
        }

        return await SetStatusAsync(userId, postId, PostActive);
    }

    public async Task<bool> DeleteAsync(long userId, long postId)
    {
        using var connection = await _db.OpenAsync();
        var row = await LoadPostAsync(connection, postId);
        if (row is null || !await IsOwnerAsync(connection, userId, row.HomeId))
        {
            return false;
        }

        await connection.ExecuteAsync("DELETE FROM housing_posts WHERE id = @postId", new { postId });
        return true;
    }

    // ------------------------------------------------------------ bookings

    // ------------------------------------------------------------ reports

    // Anyone but the post's own home can report it once while a report is open
    // (housing_reports in Admin.sql). Admins see it on the moderation page.
    public async Task<(bool Ok, string Message)> ReportAsync(long userId, long postId, ReportHousingPostDto dto)
    {
        using var connection = await _db.OpenAsync();

        var reasonId = await connection.ExecuteScalarAsync<short?>(
            "SELECT id FROM housing_report_reasons WHERE lower(name) = lower(@reason)",
            new { reason = (dto.Reason ?? string.Empty).Trim() });
        if (reasonId is null)
        {
            return (false, "Pick a reason for the report.");
        }

        var row = await connection.QuerySingleOrDefaultAsync<(long HomeId, short Status)>(
            "SELECT home_id, status FROM housing_posts WHERE id = @postId", new { postId });
        if (row == default || row.Status != PostActive)
        {
            return (false, "That post is no longer live.");
        }

        if (await IsOwnerAsync(connection, userId, row.HomeId))
        {
            return (false, "You cannot report your own post.");
        }

        var alreadyOpen = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM housing_reports WHERE post_id = @postId AND reported_by_user_id = @userId AND state = 1)",
            new { postId, userId });
        if (alreadyOpen)
        {
            return (false, "You already reported this post.");
        }

        var details = (dto.Details ?? string.Empty).Trim();
        await connection.ExecuteAsync(
            "INSERT INTO housing_reports (post_id, reported_by_user_id, reason_id, details) VALUES (@postId, @userId, @reasonId, @details)",
            new { postId, userId, reasonId, details = details.Length == 0 ? null : details[..Math.Min(300, details.Length)] });
        return (true, "Report submitted.");
    }

    // A seeker asks for a seat. Not on a closed post, not on their own home's post,
    // and not twice while an earlier request is still open.
    public async Task<(bool Ok, string Message)> RequestBookingAsync(long userId, long postId, string? message)
    {
        using var connection = await _db.OpenAsync();
        var row = await LoadPostAsync(connection, postId);
        if (row is null || row.Status != PostActive)
        {
            return (false, "This post is no longer open.");
        }

        // The detail page also disables its button at zero seats, but this check is
        // authoritative: another person may have joined after the seeker opened it.
        if (row.SeatsAvailable <= 0)
        {
            return (false, "This home no longer has a seat available.");
        }

        if (await HasHomeAsync(connection, userId))
        {
            return (false, "You already live in a home. Leave it before booking a seat somewhere else.");
        }

        if (row.VerifiedOnly && !await IsVerifiedAsync(connection, userId))
        {
            return (false, "This post is for verified accounts only.");
        }

        if (!await MatchesPersonalRequirementsAsync(connection, userId, postId))
        {
            return (false, "Your profile does not meet this listing's requirements.");
        }

        var open = await connection.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS (SELECT 1 FROM housing_bookings
                             WHERE post_id = @postId AND requester_user_id = @userId AND status IN (@pending, @accepted))",
            new { postId, userId, pending = BookingPending, accepted = BookingAccepted });
        if (open)
        {
            return (false, "You already have a request on this post.");
        }

        await connection.ExecuteAsync(
            "INSERT INTO housing_bookings (post_id, requester_user_id, message) VALUES (@postId, @userId, @message)",
            new { postId, userId, message = Trimmed(message) });

        return (true, "Request sent.");
    }

    // Owner's list of everyone who asked. Never carries contact.
    public async Task<List<BookingRequesterDto>?> GetRequestersAsync(long userId, long postId)
    {
        using var connection = await _db.OpenAsync();
        var row = await LoadPostAsync(connection, postId);
        if (row is null || !await IsOwnerAsync(connection, userId, row.HomeId))
        {
            return null;
        }

        var rows = await connection.QueryAsync<BookingRequesterDto>(
            @"SELECT b.id::text AS BookingId, u.full_name AS RequesterName, b.requested_at_utc AS RequestedAtUtc,
                     b.status - 1 AS Status, b.message AS Message
              FROM housing_bookings b
              JOIN users u ON u.id = b.requester_user_id
              WHERE b.post_id = @postId
              ORDER BY b.requested_at_utc DESC",
            new { postId });
        return rows.ToList();
    }

    public async Task<(bool Ok, string Message)> AcceptBookingAsync(long userId, long bookingId)
    {
        using var connection = await _db.OpenAsync();
        var booking = await LoadBookingForOwnerAsync(connection, userId, bookingId);
        if (booking is null || booking.Status != BookingPending)
        {
            return (false, "That request is no longer waiting.");
        }

        // Lock the home while deciding. An accepted request reserves one of its
        // currently free seats until the requester actually joins, so two managers
        // cannot promise the same last seat at once.
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        await connection.ExecuteAsync("SELECT id FROM homes WHERE id = @homeId FOR UPDATE", new { booking.HomeId }, transaction);
        var alreadyReservedForRequester = await connection.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS (
                SELECT 1
                  FROM housing_bookings b
                  JOIN housing_posts p ON p.id = b.post_id
                 WHERE p.home_id = @homeId AND b.requester_user_id = @requesterUserId
                   AND b.status = @accepted AND b.id <> @bookingId)",
            new { booking.HomeId, booking.RequesterUserId, accepted = BookingAccepted, bookingId }, transaction);
        if (alreadyReservedForRequester)
        {
            return (false, "This applicant already has an accepted reservation for this home.");
        }
        var unreservedSeats = await connection.ExecuteScalarAsync<int>(
            @"SELECT GREATEST(0,
                    COALESCE((SELECT max_occupants FROM home_capacity WHERE home_id = @homeId), 4)
                    - (SELECT count(*) FROM home_members WHERE home_id = @homeId AND left_at_utc IS NULL)
                    - (SELECT count(*)
                       FROM housing_bookings b
                       JOIN housing_posts p ON p.id = b.post_id
                      WHERE p.home_id = @homeId AND b.status = @accepted))",
            new { booking.HomeId, accepted = BookingAccepted }, transaction);
        if (unreservedSeats <= 0)
        {
            return (false, "There are no unreserved seats left in this home.");
        }

        var changed = await connection.ExecuteAsync(
            @"UPDATE housing_bookings
              SET status = @status, decided_at_utc = now(), decided_by_user_id = @userId
              WHERE id = @bookingId AND status = @pending",
            new { status = BookingAccepted, userId, bookingId, pending = BookingPending }, transaction);
        if (changed != 1)
        {
            return (false, "That request is no longer waiting.");
        }

        transaction.Commit();

        return (true, "Request accepted. You can now see each other's contact details.");
    }

    public async Task<(bool Ok, string Message)> RejectBookingAsync(long userId, long bookingId, string? reply)
    {
        using var connection = await _db.OpenAsync();
        var booking = await LoadBookingForOwnerAsync(connection, userId, bookingId);
        if (booking is null || booking.Status != BookingPending)
        {
            return (false, "That request is no longer waiting.");
        }

        await connection.ExecuteAsync(
            @"UPDATE housing_bookings
              SET status = @status, reply_message = @reply, decided_at_utc = now(), decided_by_user_id = @userId
              WHERE id = @bookingId",
            new { status = BookingRejected, reply = Trimmed(reply), userId, bookingId });

        return (true, "Request rejected.");
    }

    // An acceptance holds a real seat. A manager can release it if the move-in
    // falls through, returning the booking to a final declined state and making
    // the seat available for a different applicant.
    public async Task<(bool Ok, string Message)> ReleaseBookingAsync(long userId, long bookingId, string? reply)
    {
        using var connection = await _db.OpenAsync();
        var booking = await LoadBookingForOwnerAsync(connection, userId, bookingId);
        if (booking is null || booking.Status != BookingAccepted)
        {
            return (false, "That reservation is no longer active.");
        }

        var note = Trimmed(reply) ?? "The manager released this reservation. Please continue your search.";
        var changed = await connection.ExecuteAsync(
            @"UPDATE housing_bookings
              SET status = @status, reply_message = @reply, decided_at_utc = now(), decided_by_user_id = @userId
              WHERE id = @bookingId AND status = @accepted",
            new { status = BookingRejected, reply = note, userId, bookingId, accepted = BookingAccepted });

        return changed == 1
            ? (true, "Reservation released. The seat is available again.")
            : (false, "That reservation is no longer active.");
    }

    public async Task<(bool Ok, string Message)> WithdrawBookingAsync(long userId, long bookingId)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync(
            @"UPDATE housing_bookings
              SET status = @withdrawn, decided_at_utc = now()
              WHERE id = @bookingId AND requester_user_id = @userId AND status = @pending",
            new { withdrawn = BookingWithdrawn, bookingId, userId, pending = BookingPending });

        return changed == 1 ? (true, "Request withdrawn.") : (false, "That request cannot be withdrawn.");
    }

    // The one place contact details leave the database. Only on an accepted
    // booking, and only to the two sides of it: the seeker gets the poster's
    // contact, the owner gets the seeker's.
    public async Task<ContactDisclosureDto?> GetBookingContactAsync(long userId, long bookingId)
    {
        using var connection = await _db.OpenAsync();
        var booking = await connection.QueryFirstOrDefaultAsync<BookingRow>(
            @"SELECT b.id AS Id, b.post_id AS PostId, b.requester_user_id AS RequesterUserId, b.status AS Status,
                     b.decided_by_user_id AS DecidedByUserId,
                     p.home_id AS HomeId, p.posted_by_user_id AS PostedByUserId
              FROM housing_bookings b
              JOIN housing_posts p ON p.id = b.post_id
              WHERE b.id = @bookingId",
            new { bookingId });

        if (booking is null || booking.Status != BookingAccepted)
        {
            return null;
        }

        long? showUserId = null;
        if (booking.RequesterUserId == userId)
        {
            showUserId = booking.DecidedByUserId ?? booking.PostedByUserId;
        }
        else if (await IsOwnerAsync(connection, userId, booking.HomeId))
        {
            showUserId = booking.RequesterUserId;
        }

        if (showUserId is null)
        {
            return null;
        }

        return await connection.QueryFirstOrDefaultAsync<ContactDisclosureDto>(
            "SELECT full_name AS Name, email AS Email, phone_number AS Phone FROM users WHERE id = @id",
            new { id = showUserId });
    }

    public async Task<List<MyBookingDto>> GetMyBookingsAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var bookings = (await connection.QueryAsync<MyBookingRow>(
            @"SELECT b.id AS Id, b.post_id AS PostId, b.status AS Status, b.requested_at_utc AS RequestedAtUtc,
                     b.message AS Message, b.reply_message AS ReplyMessage, h.name AS HomeName,
                     COALESCE(d.full_name, u.full_name) AS ManagerName
              FROM housing_bookings b
              JOIN housing_posts p ON p.id = b.post_id
              JOIN homes h ON h.id = p.home_id
              JOIN users u ON u.id = p.posted_by_user_id
              LEFT JOIN users d ON d.id = b.decided_by_user_id
              WHERE b.requester_user_id = @userId
              ORDER BY b.requested_at_utc DESC",
            new { userId })).ToList();

        if (bookings.Count == 0)
        {
            return new List<MyBookingDto>();
        }

        var postIds = bookings.Select(b => b.PostId).Distinct().ToArray();
        var posts = (await connection.QueryAsync<PostRow>(
            $"{PostSelect} WHERE p.id = ANY(@postIds)", new { postIds })).ToDictionary(p => p.Id);
        var images = await LoadImagesAsync(connection, postIds);

        return bookings.Select(b => new MyBookingDto
        {
            BookingId = b.Id.ToString(),
            Post = posts.TryGetValue(b.PostId, out var post) ? ToSummary(post, images) : new HousingPostSummaryDto(),
            Status = (BookingStatus)(b.Status - 1),
            RequestedAtUtc = b.RequestedAtUtc,
            Message = b.Message,
            ReplyMessage = b.ReplyMessage,
            ManagerName = b.Status == BookingAccepted ? b.ManagerName : null,
            HomeName = b.HomeName
        }).ToList();
    }

    // ------------------------------------------------------------ helpers

    private static string? Validate(string title, string description, decimal rent, EligibilityDto eligibility)
    {
        title = (title ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();

        if (title.Length is < 4 or > 150)
        {
            return "Title should be 4-150 characters.";
        }

        if (description.Length < 20 || description.Length > 4000)
        {
            return "Description should be 20-4000 characters.";
        }

        if (rent < 0 || rent > 1_000_000)
        {
            return "Enter a rent between 0 and 10,00,000.";
        }

        if (eligibility.MinAge is { } min && eligibility.MaxAge is { } max && min > max)
        {
            return "Minimum age cannot be higher than maximum age.";
        }

        return null;
    }

    private async Task<bool> SetStatusAsync(long userId, long postId, short status)
    {
        using var connection = await _db.OpenAsync();
        var row = await LoadPostAsync(connection, postId);
        if (row is null || !await IsOwnerAsync(connection, userId, row.HomeId))
        {
            return false;
        }

        await connection.ExecuteAsync(
            @"UPDATE housing_posts
              SET status = @status, updated_at_utc = now(),
                  closed_at_utc = CASE WHEN @status = @active THEN NULL ELSE now() END
              WHERE id = @postId",
            new { status, active = PostActive, postId });
        return true;
    }

    private static Task<PostRow?> LoadPostAsync(IDbConnection connection, long postId) =>
        connection.QueryFirstOrDefaultAsync<PostRow>($"{PostSelect} WHERE p.id = @postId", new { postId });

    private static Task<BookingRow?> LoadBookingForOwnerAsync(IDbConnection connection, long userId, long bookingId) =>
        connection.QueryFirstOrDefaultAsync<BookingRow>(
            @"SELECT b.id AS Id, b.post_id AS PostId, b.requester_user_id AS RequesterUserId, b.status AS Status,
                     p.home_id AS HomeId, p.posted_by_user_id AS PostedByUserId
              FROM housing_bookings b
              JOIN housing_posts p ON p.id = b.post_id
              JOIN home_members hm ON hm.home_id = p.home_id AND hm.user_id = @userId
                                   AND hm.left_at_utc IS NULL AND hm.role IN (@manager, @coManager)
              WHERE b.id = @bookingId",
            new { userId, bookingId, manager = HomeService.RoleManager, coManager = HomeService.RoleCoManager });

    // Owner = manager or co-manager of the post's home.
    private static Task<bool> IsOwnerAsync(IDbConnection connection, long userId, long homeId) =>
        connection.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS (SELECT 1 FROM home_members
                             WHERE home_id = @homeId AND user_id = @userId AND left_at_utc IS NULL
                               AND role IN (@manager, @coManager))",
            new { homeId, userId, manager = HomeService.RoleManager, coManager = HomeService.RoleCoManager });

    private static Task<bool> HasHomeAsync(IDbConnection connection, long userId) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM home_members WHERE user_id = @userId AND left_at_utc IS NULL)",
            new { userId });

    private static Task<bool> HasBookingAsync(IDbConnection connection, long userId, long postId) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM housing_bookings WHERE post_id = @postId AND requester_user_id = @userId)",
            new { postId, userId });

    private static Task<bool> IsVerifiedAsync(IDbConnection connection, long userId) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT COALESCE((SELECT is_verified FROM user_additional_profile_info WHERE user_id = @userId), false)", new { userId });

    // Older profiles can still see an unconstrained listing. Once a listing has
    // a personal requirement, an explicit matching profile value is required.
    private const string PersonalRequirementsMatchSql = @"
        ((r.gender IS NULL AND r.occupation IS NULL AND r.min_age IS NULL AND r.max_age IS NULL
        ((r.gender IS NULL AND r.occupation IS NULL AND (r.min_age IS NULL OR r.min_age <= 0) AND (r.max_age IS NULL OR r.max_age <= 0)
          AND COALESCE(r.non_smoker_only, false) = false AND COALESCE(r.non_drinker_only, false) = false)
         OR EXISTS (
            SELECT 1 FROM user_additional_profile_info profile
             WHERE profile.user_id = @userId
               AND (r.gender IS NULL OR profile.gender = r.gender)
               AND (r.occupation IS NULL
                    OR r.occupation = 2
                    OR (r.occupation = 0 AND profile.occupation ILIKE 'Student%')
                    OR (r.occupation = 1 AND profile.occupation = 'Job holder'))
               AND (r.min_age IS NULL OR (profile.date_of_birth IS NOT NULL
               AND (r.min_age IS NULL OR r.min_age <= 0 OR (profile.date_of_birth IS NOT NULL
                    AND EXTRACT(YEAR FROM age(current_date, profile.date_of_birth)) >= r.min_age))
               AND (r.max_age IS NULL OR (profile.date_of_birth IS NOT NULL
               AND (r.max_age IS NULL OR r.max_age <= 0 OR (profile.date_of_birth IS NOT NULL
                    AND EXTRACT(YEAR FROM age(current_date, profile.date_of_birth)) <= r.max_age))
               AND (COALESCE(r.non_smoker_only, false) = false OR profile.is_smoker = false)
               AND (COALESCE(r.non_drinker_only, false) = false OR profile.is_drinker = false)))";

    private static Task<bool> MatchesPersonalRequirementsAsync(IDbConnection connection, long userId, long postId) =>
        connection.ExecuteScalarAsync<bool>(
            $@"SELECT {PersonalRequirementsMatchSql}
                FROM housing_posts p
                LEFT JOIN housing_post_requirements r ON r.post_id = p.id
               WHERE p.id = @postId",
            new { userId, postId });

    private static Task<int> FreeSeatsAsync(IDbConnection connection, long homeId) =>
        connection.ExecuteScalarAsync<int>(
            @"SELECT GREATEST(0, COALESCE((SELECT max_occupants FROM home_capacity WHERE home_id = @homeId), 4) -
                     (SELECT count(*) FROM home_members WHERE home_id = @homeId AND left_at_utc IS NULL) -
                     (SELECT count(*)
                        FROM housing_bookings b
                        JOIN housing_posts p ON p.id = b.post_id
                       WHERE p.home_id = @homeId AND b.status = @accepted))",
            new { homeId, accepted = BookingAccepted });

    private static Task SaveRequirementsAsync(IDbConnection connection, IDbTransaction transaction, long postId, EligibilityDto e) =>
        connection.ExecuteAsync(
            @"INSERT INTO housing_post_requirements
                  (post_id, gender, occupation, min_age, max_age, verified_only, non_smoker_only, non_drinker_only)
              VALUES (@postId, @gender, @occupation, @minAge, @maxAge, @verifiedOnly, @nonSmokerOnly, @nonDrinkerOnly)",
            new
            {
                postId,
                gender = e.Gender is { } g ? (short?)g : null,
                occupation = e.Occupation is { } o ? (short?)o : null,
                minAge = e.MinAge is { } min ? (short?)min : null,
                maxAge = e.MaxAge is { } max ? (short?)max : null,
                minAge = e.MinAge is { } min && min > 0 ? (short?)min : null,
                maxAge = e.MaxAge is { } max && max > 0 ? (short?)max : null,
                verifiedOnly = e.VerifiedOnly,
                nonSmokerOnly = e.NonSmokerOnly,
                nonDrinkerOnly = e.NonDrinkerOnly
            },
            transaction);

    // Same rule as the marketplace: links already online are kept, data URLs
    // picked in the browser are uploaded, anything else is dropped.
    private async Task<(List<string> Urls, string? Error)> StoreImagesAsync(IReadOnlyList<string> images)
    {
        var urls = new List<string>();
        foreach (var image in images.Take(MaxImages))
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                continue;
            }

            if (image.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                image.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(image);
                continue;
            }

            if (!image.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var comma = image.IndexOf(',');
            if (comma < 0)
            {
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(image[(comma + 1)..]);
            }
            catch (FormatException)
            {
                continue;
            }

            var contentType = image[5..comma].Split(';')[0];
            using var stream = new MemoryStream(bytes);
            var (url, error) = await _uploader.UploadAsync(stream, "housing.jpg", contentType);
            if (url is null)
            {
                return (urls, error ?? "Could not upload a photo.");
            }

            urls.Add(url);
        }

        return (urls, null);
    }

    private static async Task SaveImagesAsync(IDbConnection connection, IDbTransaction transaction, long postId, List<string> urls)
    {
        for (var i = 0; i < urls.Count; i++)
        {
            await connection.ExecuteAsync(
                "INSERT INTO housing_post_images (post_id, image_url, sort_order) VALUES (@postId, @url, @order)",
                new { postId, url = urls[i], order = (short)i },
                transaction);
        }
    }

    private static async Task<Dictionary<long, List<string>>> LoadImagesAsync(IDbConnection connection, IEnumerable<long> postIds)
    {
        var ids = postIds.Distinct().ToArray();
        var result = new Dictionary<long, List<string>>();
        if (ids.Length == 0)
        {
            return result;
        }

        var rows = await connection.QueryAsync<ImageRow>(
            "SELECT post_id AS PostId, image_url AS Url FROM housing_post_images WHERE post_id = ANY(@ids) ORDER BY post_id, sort_order",
            new { ids });

        foreach (var row in rows)
        {
            if (!result.TryGetValue(row.PostId, out var list))
            {
                list = new List<string>();
                result[row.PostId] = list;
            }
            list.Add(row.Url);
        }

        return result;
    }

    private static string? Trimmed(string? text)
    {
        var value = (text ?? string.Empty).Trim();
        return value.Length == 0 ? null : value[..Math.Min(value.Length, 500)];
    }

    private static HousingPostSummaryDto ToSummary(PostRow r, Dictionary<long, List<string>> images) => new()
    {
        Id = r.Id.ToString(),
        Title = r.Title,
        ListingType = (ListingType)r.ListingTypeId,
        SeatsAvailable = r.SeatsAvailable,
        MonthlyRent = r.MonthlyRent,
        AreaName = r.AreaName,
        Division = r.Division,
        Status = ToPostStatus(r.Status),
        CreatedAtUtc = r.CreatedAtUtc,
        ImageUrls = images.TryGetValue(r.Id, out var list) ? list : new List<string>()
    };

    private static PostStatus ToPostStatus(short status) => status switch
    {
        PostClosed => PostStatus.Closed,
        PostFilled => PostStatus.Filled,
        _ => PostStatus.Active
    };

    private static HousingPostDetailDto ToDetail(PostRow r, Dictionary<long, List<string>> images, bool isMine) => new()
    {
        Id = r.Id.ToString(),
        Title = r.Title,
        Description = r.Description,
        ListingType = (ListingType)r.ListingTypeId,
        SeatsAvailable = r.SeatsAvailable,
        MonthlyRent = r.MonthlyRent,
        AreaName = r.AreaName,
        Division = r.Division,
        Status = ToPostStatus(r.Status),
        CreatedAtUtc = r.CreatedAtUtc,
        IsMine = isMine,
        ImageUrls = images.TryGetValue(r.Id, out var list) ? list : new List<string>(),
        Eligibility = new EligibilityDto
        {
            Gender = r.Gender is { } g ? (Gender)g : null,
            Occupation = r.Occupation is { } o ? (Occupation)o : null,
            MinAge = r.MinAge,
            MaxAge = r.MaxAge,
            VerifiedOnly = r.VerifiedOnly,
            NonSmokerOnly = r.NonSmokerOnly,
            NonDrinkerOnly = r.NonDrinkerOnly
        }
    };

    private sealed class PostRow
    {
        public long Id { get; set; }
        public long HomeId { get; set; }
        public long PostedByUserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public short ListingTypeId { get; set; }
        public decimal MonthlyRent { get; set; }
        public short Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public int SeatsAvailable { get; set; }
        public short? Gender { get; set; }
        public short? Occupation { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public bool VerifiedOnly { get; set; }
        public bool NonSmokerOnly { get; set; }
        public bool NonDrinkerOnly { get; set; }
    }

    private sealed class BookingRow
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public long RequesterUserId { get; set; }
        public short Status { get; set; }
        public long? DecidedByUserId { get; set; }
        public long HomeId { get; set; }
        public long PostedByUserId { get; set; }
    }

    private sealed class MyBookingRow
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public short Status { get; set; }
        public DateTime RequestedAtUtc { get; set; }
        public string? Message { get; set; }
        public string? ReplyMessage { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public string HomeName { get; set; } = string.Empty;
    }

    private sealed class CountRow
    {
        public long PostId { get; set; }
        public int Total { get; set; }
        public int Pending { get; set; }
    }

    private sealed class ImageRow
    {
        public long PostId { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}

using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Helpers;

namespace Nestify.Api.Helpers;

// The helper's own side of Domestic_Help.sql: her profile, the weekly board,
// the schedule, the requests she answers and the reviews she replies to.
public sealed class HelperWorkspaceService
{
    public const int StartHour = 6;
    public const int EndHour = 24;

    private const short Requested = (short)EngagementStatus.Requested;
    private const short Active = (short)EngagementStatus.Active;
    private const short Completed = (short)EngagementStatus.Completed;
    private const short Declined = (short)EngagementStatus.Declined;

    private readonly DbConnectionFactory _db;

    public HelperWorkspaceService(DbConnectionFactory db)
    {
        _db = db;
    }

    // ------------------------------------------------------------ profile

    public async Task<HelperProfileDto?> GetProfileAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        await EnsureProfileAsync(connection, userId);
        return await ReadProfileAsync(connection, userId);
    }

    public async Task<HelperNavDto> GetNavAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var helperId = await EnsureProfileAsync(connection, userId);
        var row = await connection.QuerySingleAsync<NavRow>("""
            SELECT u.full_name AS Name, hp.photo_url AS PhotoUrl,
                   (SELECT count(*)::int FROM service_engagements e WHERE e.helper_profile_id = hp.id AND e.status = @requested) AS PendingRequestCount,
                   hp.monthly_rate > 0
                       AND EXISTS (SELECT 1 FROM helper_services s WHERE s.helper_profile_id = hp.id)
                       AND EXISTS (SELECT 1 FROM helper_addresses a WHERE a.helper_profile_id = hp.id) AS IsComplete
            FROM domestic_helper_profiles hp
            JOIN users u ON u.id = hp.user_id
            WHERE hp.id = @helperId
            """, new { helperId, requested = Requested });

        return new HelperNavDto
        {
            IsProfileComplete = row.IsComplete,
            Name = row.Name,
            PhotoUrl = row.PhotoUrl,
            PendingRequestCount = row.PendingRequestCount
        };
    }

    // Sign-up only collects the users row. The profile page fills the rest in
    // one section at a time, so a save may still leave the profile incomplete;
    // she switches herself on for browsing once everything is there.
    public async Task<(HelperProfileDto? Data, string? Error)> UpdateProfileAsync(long userId, HelperProfileFormDto form)
    {
        var error = Validate(form);
        if (error is not null)
        {
            return (null, error);
        }

        using var connection = await _db.OpenAsync();
        var helperId = await EnsureProfileAsync(connection, userId);

        using var transaction = connection.BeginTransaction();

        await UpdateUserAsync(connection, transaction, userId, form);

        await connection.ExecuteAsync("""
            UPDATE domestic_helper_profiles
            SET headline = @headline, bio = @bio, languages = @languages, years_experience = @years,
                monthly_rate = @rate, updated_at_utc = now()
            WHERE id = @helperId
            """, new
        {
            helperId,
            headline = form.Headline.Trim(),
            bio = NullIfBlank(form.Bio),
            languages = form.Languages.Trim(),
            years = form.ExperienceYears,
            rate = form.MonthlyRate
        }, transaction);

        // No area picked yet means the address section has not been filled in;
        // leave whatever is stored alone.
        if (form.UpazilaId is not null)
        {
            await connection.ExecuteAsync("""
                INSERT INTO helper_addresses (helper_profile_id, upazila_id, address_line, latitude, longitude)
                VALUES (@helperId, @upazilaId, @address, @lat, @lng)
                ON CONFLICT (helper_profile_id) DO UPDATE
                SET upazila_id = EXCLUDED.upazila_id, address_line = EXCLUDED.address_line,
                    latitude = EXCLUDED.latitude, longitude = EXCLUDED.longitude, updated_at_utc = now()
                """, new { helperId, upazilaId = form.UpazilaId, address = form.AddressLine.Trim(), lat = form.Latitude, lng = form.Longitude }, transaction);
        }

        await connection.ExecuteAsync("DELETE FROM helper_services WHERE helper_profile_id = @helperId", new { helperId }, transaction);
        await ReplaceServicesAsync(connection, transaction, helperId, form.Services);

        transaction.Commit();
        return (await ReadProfileAsync(connection, userId), null);
    }

    // Every helper account gets a placeholder row the first time she opens the
    // workspace: rate 0, paused, no address. Browse skips it until she fills
    // the profile in. The board starts with every day but Friday, 8 AM to 6 PM.
    private static async Task<long> EnsureProfileAsync(IDbConnection connection, long userId)
    {
        var existing = await connection.ExecuteScalarAsync<long?>(
            "SELECT id FROM domestic_helper_profiles WHERE user_id = @userId", new { userId });
        if (existing is not null)
        {
            return existing.Value;
        }

        using var transaction = connection.BeginTransaction();
        var helperId = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO domestic_helper_profiles (user_id, monthly_rate, is_active)
            VALUES (@userId, 0, false)
            RETURNING id
            """, new { userId }, transaction);

        for (var day = 0; day < 7; day++)
        {
            if (day == (int)DayOfWeek.Friday) continue;
            for (var hour = 8; hour < 18; hour++)
            {
                await connection.ExecuteAsync(
                    "INSERT INTO helper_weekly_availability (helper_profile_id, day_of_week, hour) VALUES (@helperId, @day, @hour)",
                    new { helperId, day = (short)day, hour = (short)hour }, transaction);
            }
        }

        transaction.Commit();
        return helperId;
    }

    public async Task<HelperProfileDto?> SetPhotoAsync(long userId, string url)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync(
            "UPDATE domestic_helper_profiles SET photo_url = @url, updated_at_utc = now() WHERE user_id = @userId",
            new { userId, url });
        return changed == 0 ? null : await ReadProfileAsync(connection, userId);
    }

    private static string? Validate(HelperProfileFormDto form)
    {
        if (string.IsNullOrWhiteSpace(form.FullName)) return "Your name can't be empty.";
        if (string.IsNullOrWhiteSpace(form.PhoneNumber)) return "Add a phone number so clients can reach you.";
        if (form.MonthlyRate < 0) return "The monthly rate can't be negative.";
        if (form.ExperienceYears is < 0 or > 60) return "Years of experience looks wrong.";
        if (form.UpazilaId is not null && string.IsNullOrWhiteSpace(form.AddressLine)) return "Write your address.";
        if (form.UpazilaId is null && !string.IsNullOrWhiteSpace(form.AddressLine)) return "Pick the area where you live.";
        if (form.Latitude is < -90 or > 90 || form.Longitude is < -180 or > 180) return "Drop the pin on the map where you live.";
        if ((form.Headline ?? string.Empty).Length > 120) return "Keep the headline under 120 characters.";
        if ((form.Bio ?? string.Empty).Length > 1000) return "Keep the bio under 1000 characters.";
        if ((form.Languages ?? string.Empty).Length > 120) return "Keep the languages under 120 characters.";
        return null;
    }

    private static Task UpdateUserAsync(IDbConnection connection, IDbTransaction transaction, long userId, HelperProfileFormDto form) =>
        connection.ExecuteAsync("UPDATE users SET full_name = @name, phone_number = @phone WHERE id = @userId",
            new { userId, name = form.FullName.Trim(), phone = form.PhoneNumber.Trim() }, transaction);

    private static async Task ReplaceServicesAsync(IDbConnection connection, IDbTransaction transaction, long helperId, List<ServiceType> services)
    {
        foreach (var service in services.Distinct())
        {
            await connection.ExecuteAsync(
                "INSERT INTO helper_services (helper_profile_id, service_type) VALUES (@helperId, @type)",
                new { helperId, type = (short)service }, transaction);
        }
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static async Task<HelperProfileDto?> ReadProfileAsync(IDbConnection connection, long userId)
    {
        var row = await connection.QuerySingleOrDefaultAsync<ProfileRow>("""
            SELECT hp.id, u.full_name AS FullName, u.email, u.phone_number AS PhoneNumber, u.created_at_utc AS JoinedAtUtc,
                   hp.photo_url AS PhotoUrl, hp.headline, coalesce(hp.bio, '') AS Bio, hp.languages,
                   hp.monthly_rate AS MonthlyRate, hp.years_experience AS ExperienceYears,
                   coalesce(hp.average_rating, 0) AS RatingAverage, hp.review_count AS RatingCount,
                   hp.is_verified AS IsVerified, hp.is_active AS IsActive,
                   d.division_id AS DivisionId, up.district_id AS DistrictId, a.upazila_id AS UpazilaId,
                   coalesce(up.name, '') AS AreaName, coalesce(a.address_line, '') AS AddressLine,
                   coalesce(a.latitude, 23.7806) AS Latitude, coalesce(a.longitude, 90.4074) AS Longitude,
                   a.helper_profile_id IS NOT NULL AS HasAddress,
                   (SELECT count(*)::int FROM service_engagements e WHERE e.helper_profile_id = hp.id AND e.status = @completed) AS CompletedCount,
                   (SELECT count(*)::int FROM service_engagements e WHERE e.helper_profile_id = hp.id AND e.status = @active) AS ActiveCount,
                   (SELECT count(*)::int FROM helper_weekly_availability w WHERE w.helper_profile_id = hp.id) AS OpenHoursPerWeek
            FROM domestic_helper_profiles hp
            JOIN users u ON u.id = hp.user_id
            LEFT JOIN helper_addresses a ON a.helper_profile_id = hp.id
            LEFT JOIN upazilas up ON up.id = a.upazila_id
            LEFT JOIN districts d ON d.id = up.district_id
            WHERE hp.user_id = @userId
            """, new { userId, completed = Completed, active = Active });

        if (row is null)
        {
            return null;
        }

        var services = await HelperService.LoadServicesAsync(connection, new List<long> { row.Id });

        var myServices = services.GetValueOrDefault(row.Id, new List<ServiceType>());

        return new HelperProfileDto
        {
            Id = row.Id.ToString(),
            IsComplete = row.HasAddress && row.MonthlyRate > 0 && myServices.Count > 0,
            FullName = row.FullName,
            Email = row.Email,
            PhoneNumber = row.PhoneNumber,
            JoinedAtUtc = row.JoinedAtUtc,
            PhotoUrl = row.PhotoUrl,
            Headline = row.Headline,
            Bio = row.Bio,
            Languages = row.Languages,
            Services = myServices,
            MonthlyRate = row.MonthlyRate,
            ExperienceYears = row.ExperienceYears,
            RatingAverage = (double)row.RatingAverage,
            RatingCount = row.RatingCount,
            IsVerified = row.IsVerified,
            IsAcceptingBookings = row.IsActive,
            DivisionId = row.DivisionId,
            DistrictId = row.DistrictId,
            UpazilaId = row.UpazilaId,
            AreaName = row.AreaName,
            AddressLine = row.AddressLine,
            Latitude = (double)row.Latitude,
            Longitude = (double)row.Longitude,
            CompletedCount = row.CompletedCount,
            ActiveCount = row.ActiveCount,
            OpenHoursPerWeek = row.OpenHoursPerWeek,
            Verification = await ReadVerificationAsync(connection, userId, row.IsVerified)
        };
    }

    // The latest helper verification request decides the badge on the profile.
    internal static async Task<HelperVerificationStatusDto> ReadVerificationAsync(IDbConnection connection, long userId, bool isVerified)
    {
        var latest = await connection.QuerySingleOrDefaultAsync<VerificationRow>("""
            SELECT status, submitted_at_utc AS SubmittedAtUtc, rejection_reason AS RejectionReason
            FROM verification_requests
            WHERE user_id = @userId AND subject_type = 2 AND status <> 4
            ORDER BY submitted_at_utc DESC
            LIMIT 1
            """, new { userId });

        if (isVerified)
        {
            return new HelperVerificationStatusDto { State = HelperVerificationState.Verified, SubmittedAtUtc = latest?.SubmittedAtUtc };
        }

        return latest switch
        {
            { Status: 1 } => new HelperVerificationStatusDto { State = HelperVerificationState.Pending, SubmittedAtUtc = latest.SubmittedAtUtc },
            { Status: 3 } => new HelperVerificationStatusDto { State = HelperVerificationState.Rejected, SubmittedAtUtc = latest.SubmittedAtUtc, RejectionReason = latest.RejectionReason },
            _ => new HelperVerificationStatusDto { State = HelperVerificationState.NotApplied }
        };
    }

    // ------------------------------------------------------------ dashboard

    public async Task<HelperWorkspaceDashboardDto?> GetDashboardAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        long? helperId = await EnsureProfileAsync(connection, userId);

        var summary = await connection.QuerySingleAsync<DashboardRow>("""
            SELECT count(*) FILTER (WHERE e.status = @requested)::int AS PendingRequestCount,
                   count(*) FILTER (WHERE e.status = @active)::int AS ActiveJobCount,
                   coalesce(sum(e.monthly_rate) FILTER (WHERE e.status = @active), 0) AS MonthlyEarnings,
                   coalesce(hp.average_rating, 0) AS RatingAverage, hp.review_count AS ReviewCount, hp.is_verified AS IsVerified,
                   hp.monthly_rate > 0
                       AND EXISTS (SELECT 1 FROM helper_services s WHERE s.helper_profile_id = hp.id)
                       AND EXISTS (SELECT 1 FROM helper_addresses a WHERE a.helper_profile_id = hp.id) AS IsProfileComplete
            FROM domestic_helper_profiles hp
            LEFT JOIN service_engagements e ON e.helper_profile_id = hp.id
            WHERE hp.id = @helperId
            GROUP BY hp.id
            """, new { helperId, requested = Requested, active = Active });

        var today = DateTime.Today;
        var weekStart = StartOfWeek(today);
        var visits = await BuildVisitsAsync(connection, helperId.Value, weekStart, weekStart.AddDays(7));
        var requests = await LoadRequestsAsync(connection, helperId.Value);

        return new HelperWorkspaceDashboardDto
        {
            PendingRequestCount = summary.PendingRequestCount,
            ActiveJobCount = summary.ActiveJobCount,
            MonthlyEarnings = summary.MonthlyEarnings,
            HoursThisWeek = visits.Count,
            VisitsThisWeek = visits.Select(v => (v.EngagementId, v.Date)).Distinct().Count(),
            RatingAverage = (double)summary.RatingAverage,
            ReviewCount = summary.ReviewCount,
            IsVerified = summary.IsVerified,
            IsProfileComplete = summary.IsProfileComplete,
            TodayVisits = visits.Where(v => v.Date == today).ToList(),
            NewRequests = requests.Take(3).ToList()
        };
    }

    // ------------------------------------------------------------ availability

    public async Task<HelperAvailabilityDto?> GetAvailabilityAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        long? helperId = await EnsureProfileAsync(connection, userId);

        return new HelperAvailabilityDto
        {
            IsAvailable = await connection.ExecuteScalarAsync<bool>("SELECT is_active FROM domestic_helper_profiles WHERE id = @helperId", new { helperId }),
            Slots = await HelperService.LoadBoardAsync(connection, helperId.Value)
        };
    }

    public async Task<string?> SaveAvailabilityAsync(long userId, HelperAvailabilityDto dto)
    {
        using var connection = await _db.OpenAsync();
        long? helperId = await EnsureProfileAsync(connection, userId);
        if (dto.Slots.Any(s => s.DayOfWeek is < 0 or > 6 || s.Hour is < StartHour or >= EndHour))
        {
            return "The board contains an hour that does not exist.";
        }
        if (dto.IsAvailable && !await IsCompleteAsync(connection, helperId.Value))
        {
            return "Fill in your services, rate and address on your profile before accepting bookings.";
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync("UPDATE domestic_helper_profiles SET is_active = @active, updated_at_utc = now() WHERE id = @helperId",
            new { helperId, active = dto.IsAvailable }, transaction);

        await connection.ExecuteAsync("DELETE FROM helper_weekly_availability WHERE helper_profile_id = @helperId", new { helperId }, transaction);

        // Booked hours stay open no matter what the board says, otherwise an
        // active engagement would sit on an hour she claims to be off.
        var open = dto.Slots.Where(s => s.IsOpen || s.IsBooked).Select(s => (s.DayOfWeek, s.Hour)).ToHashSet();
        foreach (var (day, hour) in open)
        {
            await connection.ExecuteAsync(
                "INSERT INTO helper_weekly_availability (helper_profile_id, day_of_week, hour) VALUES (@helperId, @day, @hour)",
                new { helperId, day = (short)day, hour = (short)hour }, transaction);
        }

        transaction.Commit();
        return null;
    }

    // ------------------------------------------------------------ schedule

    public async Task<HelperWorkspaceScheduleDto?> GetScheduleAsync(long userId, DateTime weekStart)
    {
        using var connection = await _db.OpenAsync();
        long? helperId = await EnsureProfileAsync(connection, userId);

        var start = StartOfWeek(weekStart);
        var visits = await BuildVisitsAsync(connection, helperId.Value, start, start.AddDays(7));

        // The next visit can be in a later week than the one on screen.
        var upcoming = await BuildVisitsAsync(connection, helperId.Value, DateTime.Today, DateTime.Today.AddDays(28));
        var now = DateTime.Now;
        var next = upcoming.FirstOrDefault(v => v.Date > now.Date || (v.Date == now.Date && v.EndHour > now.Hour));

        return new HelperWorkspaceScheduleDto { WeekStart = start, Visits = visits, NextVisit = next };
    }

    // ------------------------------------------------------------ engagements

    public async Task<HelperWorkspaceEngagementsDto?> GetEngagementsAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        long? helperId = await EnsureProfileAsync(connection, userId);

        var rows = (await connection.QueryAsync<EngagementRow>("""
            SELECT e.id, u.full_name AS ClientName, coalesce(p.profile_picture_url, '') AS ClientPhotoUrl,
                   coalesce(h.name, '') AS HomeName, coalesce(h.area_name, '') AS Area,
                   coalesce(h.address_line, p.address, '') AS Address, u.phone_number AS Phone,
                   e.monthly_rate AS MonthlyRate, e.start_date AS StartDate, e.status,
                   e.helper_completed_at_utc IS NOT NULL AS HelperMarkedComplete,
                   e.client_completed_at_utc IS NOT NULL AS ClientMarkedComplete,
                   e.completed_at_utc AS CompletedAtUtc
            FROM service_engagements e
            JOIN users u ON u.id = e.client_user_id
            LEFT JOIN user_additional_profile_info p ON p.user_id = u.id
            LEFT JOIN homes h ON h.id = e.home_id
            WHERE e.helper_profile_id = @helperId AND e.status IN (@active, @completed)
            ORDER BY e.start_date DESC NULLS LAST, e.id DESC
            """, new { helperId, active = Active, completed = Completed })).ToList();

        var ids = rows.Select(r => r.Id).ToList();
        var services = await HelperService.LoadEngagementServicesAsync(connection, ids);
        var slots = await HelperService.LoadSlotsAsync(connection, ids);

        var engagements = rows.Select(r =>
        {
            var mySlots = slots.GetValueOrDefault(r.Id, new List<EngagementSlotDto>());
            var startedOn = r.StartDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
            var endedOn = r.CompletedAtUtc?.ToLocalTime().Date ?? DateTime.Today;
            var done = CountVisits(mySlots, startedOn, endedOn, DateTime.Now);
            var planned = Math.Max(mySlots.Count * 4, done);

            return new HelperWorkspaceEngagementDto
            {
                Id = r.Id.ToString(),
                ClientName = r.ClientName,
                ClientPhotoUrl = r.ClientPhotoUrl,
                HomeName = r.HomeName,
                Area = r.Area,
                Address = r.Address,
                Phone = r.Phone,
                Services = services.GetValueOrDefault(r.Id, new List<ServiceType>()).Select(ServiceTypes.Label).ToList(),
                MonthlyRate = r.MonthlyRate,
                StartedOn = startedOn,
                Status = (EngagementStatus)r.Status,
                WeeklyHours = mySlots.Count,
                VisitsDone = done,
                VisitsPlanned = planned,
                HelperMarkedComplete = r.HelperMarkedComplete,
                ClientMarkedComplete = r.ClientMarkedComplete,
                Slots = mySlots
            };
        }).ToList();

        return new HelperWorkspaceEngagementsDto
        {
            Requests = await LoadRequestsAsync(connection, helperId.Value),
            Current = engagements.Where(e => e.Status == EngagementStatus.Active).ToList(),
            Past = engagements.Where(e => e.Status == EngagementStatus.Completed).ToList()
        };
    }

    public async Task<string?> DecideAsync(long userId, string engagementId, bool accept, string? reason)
    {
        if (!long.TryParse(engagementId, out var id))
        {
            return "Request not found.";
        }

        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason is { Length: > 500 })
        {
            reason = reason[..500];
        }

        using var connection = await _db.OpenAsync();
        long? helperId = await EnsureProfileAsync(connection, userId);

        using var transaction = connection.BeginTransaction();

        var changed = await connection.ExecuteAsync(accept
                ? """
                  UPDATE service_engagements
                  SET status = @active, helper_confirmed_at_utc = now(), start_date = CURRENT_DATE
                  WHERE id = @id AND helper_profile_id = @helperId AND status = @requested
                  """
                : """
                  UPDATE service_engagements
                  SET status = @declined, decline_reason = @reason, cancelled_at_utc = now()
                  WHERE id = @id AND helper_profile_id = @helperId AND status = @requested
                  """,
            new { id, helperId, requested = Requested, active = Active, declined = Declined, reason }, transaction);

        if (changed == 0)
        {
            transaction.Rollback();
            return "This request was already answered.";
        }

        // Accepting is the day she joins the home; the placement is what the
        // home's members review later.
        if (accept)
        {
            await connection.ExecuteAsync("""
                INSERT INTO helper_home_placements (engagement_id, helper_profile_id, home_id, joined_on)
                SELECT e.id, e.helper_profile_id, e.home_id, CURRENT_DATE
                FROM service_engagements e WHERE e.id = @id
                ON CONFLICT (engagement_id) DO NOTHING
                """, new { id }, transaction);
        }

        transaction.Commit();
        return null;
    }

    // ------------------------------------------------------------ reviews

    public async Task<HelperReviewsDto?> GetReviewsAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        long? helperId = await EnsureProfileAsync(connection, userId);

        var summary = await connection.QuerySingleAsync<(decimal Average, int Count)>(
            "SELECT coalesce(average_rating, 0), review_count FROM domestic_helper_profiles WHERE id = @helperId", new { helperId });

        var rows = (await connection.QueryAsync<HelperService.ReviewRow>(HelperService.ReviewSql + """
            WHERE r.helper_profile_id = @helperId AND NOT r.is_hidden
            ORDER BY r.created_at_utc DESC
            """, new { helperId })).ToList();

        return new HelperReviewsDto
        {
            RatingAverage = (double)summary.Average,
            RatingCount = summary.Count,
            Reviews = await HelperService.ToReviewDtosAsync(connection, rows)
        };
    }

    public async Task<string?> ReplyAsync(long userId, string reviewId, string reply)
    {
        if (!long.TryParse(reviewId, out var id))
        {
            return "Review not found.";
        }

        reply = (reply ?? string.Empty).Trim();
        if (reply.Length > 500)
        {
            reply = reply[..500];
        }

        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync("""
            UPDATE helper_reviews r
            SET reply = @reply, replied_at_utc = CASE WHEN @reply IS NULL THEN NULL ELSE now() END
            FROM domestic_helper_profiles hp
            WHERE r.id = @id AND r.helper_profile_id = hp.id AND hp.user_id = @userId
            """, new { id, userId, reply = reply.Length == 0 ? null : reply });

        return changed == 0 ? "Review not found." : null;
    }

    // ------------------------------------------------------------ visits

    // Visits are not stored: each active engagement repeats its weekly slots
    // from its start date until it is completed.
    private static async Task<List<HelperWorkspaceVisitDto>> BuildVisitsAsync(IDbConnection connection, long helperId, DateTime from, DateTime to)
    {
        var rows = (await connection.QueryAsync<VisitSourceRow>("""
            SELECT e.id, u.full_name AS ClientName, coalesce(h.area_name, '') AS Area,
                   e.start_date AS StartDate, e.completed_at_utc AS CompletedAtUtc
            FROM service_engagements e
            JOIN users u ON u.id = e.client_user_id
            LEFT JOIN homes h ON h.id = e.home_id
            WHERE e.helper_profile_id = @helperId AND e.status IN (@active, @completed)
            """, new { helperId, active = Active, completed = Completed })).ToList();

        var ids = rows.Select(r => r.Id).ToList();
        var slots = await HelperService.LoadSlotsAsync(connection, ids);
        var services = await HelperService.LoadEngagementServicesAsync(connection, ids);
        var now = DateTime.Now;

        var visits = new List<HelperWorkspaceVisitDto>();
        foreach (var row in rows)
        {
            var started = row.StartDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MaxValue;
            var ended = row.CompletedAtUtc?.ToLocalTime().Date;
            var service = services.GetValueOrDefault(row.Id, new List<ServiceType>()).Select(ServiceTypes.Label).FirstOrDefault() ?? "Visit";

            for (var date = from.Date; date < to.Date; date = date.AddDays(1))
            {
                if (date < started || (ended is not null && date > ended)) continue;

                foreach (var slot in slots.GetValueOrDefault(row.Id, new List<EngagementSlotDto>()))
                {
                    if (slot.DayOfWeek != (int)date.DayOfWeek) continue;
                    visits.Add(new HelperWorkspaceVisitDto
                    {
                        Id = $"{row.Id}-{date:yyyyMMdd}-{slot.Hour}",
                        EngagementId = row.Id.ToString(),
                        ClientName = row.ClientName,
                        Area = row.Area,
                        Service = service,
                        Date = date,
                        StartHour = slot.Hour,
                        EndHour = slot.Hour + 1,
                        IsDone = date < now.Date || (date == now.Date && slot.Hour + 1 <= now.Hour)
                    });
                }
            }
        }

        return visits.OrderBy(v => v.Date).ThenBy(v => v.StartHour).ToList();
    }

    private static int CountVisits(List<EngagementSlotDto> slots, DateTime from, DateTime to, DateTime now)
    {
        var count = 0;
        for (var date = from.Date; date <= to.Date && date <= now.Date; date = date.AddDays(1))
        {
            count += slots.Count(s => s.DayOfWeek == (int)date.DayOfWeek && (date < now.Date || s.Hour + 1 <= now.Hour));
        }
        return count;
    }

    private static async Task<List<HelperWorkspaceRequestDto>> LoadRequestsAsync(IDbConnection connection, long helperId)
    {
        var rows = (await connection.QueryAsync<RequestRow>("""
            SELECT e.id, u.full_name AS ClientName, coalesce(p.profile_picture_url, '') AS ClientPhotoUrl,
                   coalesce(p.is_verified, false) AS ClientVerified,
                   coalesce(h.name, '') AS HomeName, coalesce(h.area_name, '') AS Area,
                   e.monthly_rate AS OfferedRate, e.message, e.requested_at_utc AS RequestedAtUtc
            FROM service_engagements e
            JOIN users u ON u.id = e.client_user_id
            LEFT JOIN user_additional_profile_info p ON p.user_id = u.id
            LEFT JOIN homes h ON h.id = e.home_id
            WHERE e.helper_profile_id = @helperId AND e.status = @requested
            ORDER BY e.requested_at_utc DESC
            """, new { helperId, requested = Requested })).ToList();

        var ids = rows.Select(r => r.Id).ToList();
        var services = await HelperService.LoadEngagementServicesAsync(connection, ids);
        var slots = await HelperService.LoadSlotsAsync(connection, ids);

        return rows.Select(r => new HelperWorkspaceRequestDto
        {
            Id = r.Id.ToString(),
            ClientName = r.ClientName,
            ClientPhotoUrl = r.ClientPhotoUrl,
            ClientVerified = r.ClientVerified,
            HomeName = r.HomeName,
            Area = r.Area,
            Services = services.GetValueOrDefault(r.Id, new List<ServiceType>()).Select(ServiceTypes.Label).ToList(),
            OfferedRate = r.OfferedRate,
            Message = r.Message,
            RequestedAtUtc = r.RequestedAtUtc,
            Slots = slots.GetValueOrDefault(r.Id, new List<EngagementSlotDto>())
        }).ToList();
    }

    internal static Task<bool> IsCompleteAsync(IDbConnection connection, long helperId) =>
        connection.ExecuteScalarAsync<bool>("""
            SELECT monthly_rate > 0
                   AND EXISTS (SELECT 1 FROM helper_services s WHERE s.helper_profile_id = hp.id)
                   AND EXISTS (SELECT 1 FROM helper_addresses a WHERE a.helper_profile_id = hp.id)
            FROM domestic_helper_profiles hp WHERE hp.id = @helperId
            """, new { helperId });

    // The week is read Saturday first here.
    public static DateTime StartOfWeek(DateTime day) =>
        day.Date.AddDays(-(((int)day.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7));

    // ------------------------------------------------------------ rows

    private sealed class NavRow
    {
        public string Name { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
        public int PendingRequestCount { get; set; }
        public bool IsComplete { get; set; }
    }

    private sealed class ProfileRow
    {
        public long Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime JoinedAtUtc { get; set; }
        public string PhotoUrl { get; set; } = string.Empty;
        public string Headline { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Languages { get; set; } = string.Empty;
        public decimal MonthlyRate { get; set; }
        public int ExperienceYears { get; set; }
        public decimal RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public bool IsVerified { get; set; }
        public bool IsActive { get; set; }
        public int? DivisionId { get; set; }
        public int? DistrictId { get; set; }
        public int? UpazilaId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string AddressLine { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public bool HasAddress { get; set; }
        public int CompletedCount { get; set; }
        public int ActiveCount { get; set; }
        public int OpenHoursPerWeek { get; set; }
    }

    private sealed class VerificationRow
    {
        public short Status { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public string? RejectionReason { get; set; }
    }

    private sealed class DashboardRow
    {
        public int PendingRequestCount { get; set; }
        public int ActiveJobCount { get; set; }
        public decimal MonthlyEarnings { get; set; }
        public decimal RatingAverage { get; set; }
        public int ReviewCount { get; set; }
        public bool IsVerified { get; set; }
        public bool IsProfileComplete { get; set; }
    }

    private sealed class EngagementRow
    {
        public long Id { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhotoUrl { get; set; } = string.Empty;
        public string HomeName { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal MonthlyRate { get; set; }
        public DateOnly? StartDate { get; set; }
        public short Status { get; set; }
        public bool HelperMarkedComplete { get; set; }
        public bool ClientMarkedComplete { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }

    private sealed class VisitSourceRow
    {
        public long Id { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public DateOnly? StartDate { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }

    private sealed class RequestRow
    {
        public long Id { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhotoUrl { get; set; } = string.Empty;
        public bool ClientVerified { get; set; }
        public string HomeName { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public decimal OfferedRate { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime RequestedAtUtc { get; set; }
    }
}

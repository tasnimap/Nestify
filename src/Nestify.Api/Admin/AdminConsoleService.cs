using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Admin;

namespace Nestify.Api.Admin;

// Everything the admin console does, against the tables in Admin.sql plus the
// housing and marketplace tables it moderates. Every decision is written to
// admin_audit_log under the admin who made it.
public sealed class AdminConsoleService
{
    private const short ScopeHousing = 1;
    private const short ScopeMarketplace = 2;

    private const short ReportOpen = 1;
    private const short ReportResolved = 2;
    private const short ReportDismissed = 3;

    private const short HousingActive = 1;
    private const short ListingActive = 1;
    private const short ListingRemoved = 3;

    private const short AccountAdmin = 3;

    private readonly DbConnectionFactory _db;

    public AdminConsoleService(DbConnectionFactory db) => _db = db;

    // ------------------------------------------------------------ summary

    public async Task<AdminSummaryDto> GetSummaryAsync()
    {
        using var connection = await _db.OpenAsync();
        return await connection.QuerySingleAsync<AdminSummaryDto>(
            @"SELECT (SELECT count(*)::int FROM housing_reports WHERE state = @open)      AS OpenHousingReports,
                     (SELECT count(*)::int FROM marketplace_reports WHERE state = @open)  AS OpenMarketReports,
                     (SELECT count(*)::int FROM housing_posts p
                       WHERE p.status = @housingActive AND NOT EXISTS (
                           SELECT 1 FROM post_takedowns t
                            WHERE t.scope = @housing AND t.post_id = p.id AND t.restored_at_utc IS NULL)) AS LiveHousingPosts,
                     (SELECT count(*)::int FROM marketplace_listings WHERE status = @listingActive) AS LiveMarketItems,
                     (SELECT count(*)::int FROM post_takedowns)                          AS PostsTakenDown,
                     (SELECT count(*)::int FROM users u
                       LEFT JOIN admin_accounts a ON a.user_id = u.id
                       WHERE u.account_type = @admin AND coalesce(a.is_active, true))    AS ActiveAdmins",
            new { open = ReportOpen, housingActive = HousingActive, housing = ScopeHousing, listingActive = ListingActive, admin = AccountAdmin });
    }

    // ------------------------------------------------------------ housing

    // Live posts plus the ones an admin took down; closed and filled posts are
    // the owner's business and stay out of the console.
    public async Task<List<AdminHousingPostDto>> GetHousingPostsAsync()
    {
        using var connection = await _db.OpenAsync();
        var rows = (await connection.QueryAsync<HousingRow>(
            @"SELECT p.id, p.title, p.description, lt.name AS listing_type, p.monthly_rent_bdt AS rent,
                     p.created_at_utc AS posted_at_utc,
                     h.area_name AS area, h.division,
                     GREATEST(0, COALESCE(c.max_occupants, 4) -
                         (SELECT count(*) FROM home_members m WHERE m.home_id = h.id AND m.left_at_utc IS NULL)) AS seats,
                     u.full_name AS owner, coalesce(up.is_verified, false) AS owner_verified,
                     r.gender, r.occupation, r.min_age, r.max_age, coalesce(r.verified_only, false) AS verified_only,
                     t.reason AS removal_reason,
                     (SELECT count(*)::int FROM housing_reports hr WHERE hr.post_id = p.id) AS report_count
                FROM housing_posts p
                JOIN homes h ON h.id = p.home_id
                JOIN housing_listing_types lt ON lt.id = p.listing_type_id
                JOIN users u ON u.id = p.posted_by_user_id
                LEFT JOIN user_additional_profile_info up ON up.user_id = u.id
                LEFT JOIN home_capacity c ON c.home_id = h.id
                LEFT JOIN housing_post_requirements r ON r.post_id = p.id
                LEFT JOIN post_takedowns t ON t.scope = @housing AND t.post_id = p.id AND t.restored_at_utc IS NULL
               WHERE p.status = @active OR t.id IS NOT NULL
               ORDER BY p.created_at_utc DESC",
            new { housing = ScopeHousing, active = HousingActive })).ToList();

        var images = await LoadImagesAsync(connection, "housing_post_images", "post_id", rows.Select(r => r.Id));

        return rows.Select(r => new AdminHousingPostDto
        {
            Id = r.Id.ToString(),
            Title = r.Title,
            Description = r.Description,
            ListingType = r.ListingType,
            Seats = r.Seats,
            Area = r.Area,
            Division = r.Division,
            Owner = r.Owner,
            OwnerVerified = r.OwnerVerified,
            Eligibility = Eligibility(r),
            Rent = r.Rent,
            Photos = images.TryGetValue(r.Id, out var list) ? list : new List<string>(),
            PostedAtUtc = r.PostedAtUtc,
            ReportCount = r.ReportCount,
            State = r.RemovalReason is null ? ModeratedPostState.Live : ModeratedPostState.Removed,
            RemovalReason = r.RemovalReason
        }).ToList();
    }

    public async Task<List<AdminReportDto>> GetHousingReportsAsync()
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<ReportRow>(
            @"SELECT r.id, r.post_id, p.title AS post_title, rs.name AS reason, r.details,
                     u.full_name AS reported_by, r.raised_at_utc, r.state
                FROM housing_reports r
                JOIN housing_posts p ON p.id = r.post_id
                JOIN housing_report_reasons rs ON rs.id = r.reason_id
                JOIN users u ON u.id = r.reported_by_user_id
               ORDER BY r.state, r.raised_at_utc DESC");
        return rows.Select(r => ToReport(r, ModerationScope.Housing)).ToList();
    }

    public async Task<(bool Ok, string Message)> TakeDownHousingPostAsync(long adminId, long postId, TakedownRequestDto dto)
    {
        var reason = (dto.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
        {
            return (false, "Give a reason for the strike.");
        }

        using var connection = await _db.OpenAsync();

        var title = await connection.ExecuteScalarAsync<string?>(
            "SELECT title FROM housing_posts WHERE id = @postId AND status = @active",
            new { postId, active = HousingActive });
        if (title is null)
        {
            return (false, "That post is not live.");
        }

        if (await HasOpenTakedownAsync(connection, ScopeHousing, postId))
        {
            return (false, "That post is already taken down.");
        }

        using var transaction = connection.BeginTransaction();
        await InsertTakedownAsync(connection, transaction, ScopeHousing, postId, adminId, reason);
        await ResolveLinkedReportAsync(connection, transaction, "housing_reports", adminId, dto.ReportId, postId);
        await LogAsync(connection, transaction, adminId, "Removed post", $"Housing · {title}", reason, "danger");
        transaction.Commit();
        return (true, "Post taken down.");
    }

    public async Task<(bool Ok, string Message)> RestoreHousingPostAsync(long adminId, long postId)
    {
        using var connection = await _db.OpenAsync();
        var title = await connection.ExecuteScalarAsync<string?>(
            "SELECT title FROM housing_posts WHERE id = @postId", new { postId });
        if (title is null)
        {
            return (false, "That post no longer exists.");
        }

        using var transaction = connection.BeginTransaction();
        var changed = await RestoreTakedownAsync(connection, transaction, ScopeHousing, postId, adminId);
        if (changed == 0)
        {
            return (false, "That post is already live.");
        }

        await LogAsync(connection, transaction, adminId, "Restored post", $"Housing · {title}", null, "ok");
        transaction.Commit();
        return (true, "Post is live again.");
    }

    // ------------------------------------------------------------ marketplace

    public async Task<List<AdminMarketItemDto>> GetMarketItemsAsync()
    {
        using var connection = await _db.OpenAsync();
        var rows = (await connection.QueryAsync<MarketRow>(
            @"SELECT l.id, l.title, l.description, c.name AS category, cd.name AS condition,
                     l.price_bdt AS price, l.area_name AS area, l.posted_at_utc,
                     u.full_name AS seller, coalesce(up.is_verified, false) AS seller_verified,
                     t.reason AS removal_reason,
                     (SELECT count(*)::int FROM marketplace_reports mr WHERE mr.listing_id = l.id) AS report_count
                FROM marketplace_listings l
                JOIN marketplace_categories c ON c.id = l.category_id
                JOIN marketplace_conditions cd ON cd.id = l.condition_id
                JOIN users u ON u.id = l.seller_user_id
                LEFT JOIN user_additional_profile_info up ON up.user_id = u.id
                LEFT JOIN post_takedowns t ON t.scope = @market AND t.post_id = l.id AND t.restored_at_utc IS NULL
               WHERE l.status = @active OR t.id IS NOT NULL
               ORDER BY l.posted_at_utc DESC",
            new { market = ScopeMarketplace, active = ListingActive })).ToList();

        var images = await LoadImagesAsync(connection, "marketplace_listing_images", "listing_id", rows.Select(r => r.Id));

        return rows.Select(r => new AdminMarketItemDto
        {
            Id = r.Id.ToString(),
            Title = r.Title,
            Description = r.Description,
            Category = r.Category,
            Condition = r.Condition,
            Seller = r.Seller,
            SellerVerified = r.SellerVerified,
            Area = r.Area,
            Price = r.Price,
            Photos = images.TryGetValue(r.Id, out var list) ? list : new List<string>(),
            PostedAtUtc = r.PostedAtUtc,
            ReportCount = r.ReportCount,
            State = r.RemovalReason is null ? ModeratedPostState.Live : ModeratedPostState.Removed,
            RemovalReason = r.RemovalReason
        }).ToList();
    }

    public async Task<List<AdminReportDto>> GetMarketReportsAsync()
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<ReportRow>(
            @"SELECT r.id, r.listing_id AS post_id, l.title AS post_title, rs.name AS reason, r.details,
                     u.full_name AS reported_by, r.raised_at_utc, r.state
                FROM marketplace_reports r
                JOIN marketplace_listings l ON l.id = r.listing_id
                JOIN marketplace_report_reasons rs ON rs.id = r.reason_id
                JOIN users u ON u.id = r.reported_by_user_id
               ORDER BY r.state, r.raised_at_utc DESC");
        return rows.Select(r => ToReport(r, ModerationScope.Marketplace)).ToList();
    }

    public async Task<(bool Ok, string Message)> TakeDownMarketItemAsync(long adminId, long listingId, TakedownRequestDto dto)
    {
        var reason = (dto.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
        {
            return (false, "Give a reason for the strike.");
        }

        using var connection = await _db.OpenAsync();

        var title = await connection.ExecuteScalarAsync<string?>(
            "SELECT title FROM marketplace_listings WHERE id = @listingId AND status = @active",
            new { listingId, active = ListingActive });
        if (title is null)
        {
            return (false, "That listing is not live.");
        }

        using var transaction = connection.BeginTransaction();
        await InsertTakedownAsync(connection, transaction, ScopeMarketplace, listingId, adminId, reason);
        await connection.ExecuteAsync(
            @"UPDATE marketplace_listings
                 SET status = @removed, removed_at_utc = now(), removed_by_user_id = @adminId, updated_at_utc = now()
               WHERE id = @listingId",
            new { listingId, adminId, removed = ListingRemoved }, transaction);
        await ResolveLinkedReportAsync(connection, transaction, "marketplace_reports", adminId, dto.ReportId, listingId);
        await LogAsync(connection, transaction, adminId, "Removed post", $"Marketplace · {title}", reason, "danger");
        transaction.Commit();
        return (true, "Item taken down.");
    }

    public async Task<(bool Ok, string Message)> RestoreMarketItemAsync(long adminId, long listingId)
    {
        using var connection = await _db.OpenAsync();
        var title = await connection.ExecuteScalarAsync<string?>(
            "SELECT title FROM marketplace_listings WHERE id = @listingId", new { listingId });
        if (title is null)
        {
            return (false, "That listing no longer exists.");
        }

        using var transaction = connection.BeginTransaction();
        var changed = await RestoreTakedownAsync(connection, transaction, ScopeMarketplace, listingId, adminId);
        if (changed == 0)
        {
            return (false, "That listing is already live.");
        }

        await connection.ExecuteAsync(
            @"UPDATE marketplace_listings
                 SET status = @active, removed_at_utc = NULL, removed_by_user_id = NULL, updated_at_utc = now()
               WHERE id = @listingId",
            new { listingId, active = ListingActive }, transaction);
        await LogAsync(connection, transaction, adminId, "Restored post", $"Marketplace · {title}", null, "ok");
        transaction.Commit();
        return (true, "Item is live again.");
    }

    // ------------------------------------------------------------ reports

    public async Task<bool> DismissReportAsync(long adminId, ModerationScope scope, long reportId)
    {
        var table = scope == ModerationScope.Housing ? "housing_reports" : "marketplace_reports";
        var titleSql = scope == ModerationScope.Housing
            ? "SELECT p.title FROM housing_reports r JOIN housing_posts p ON p.id = r.post_id WHERE r.id = @reportId"
            : "SELECT l.title FROM marketplace_reports r JOIN marketplace_listings l ON l.id = r.listing_id WHERE r.id = @reportId";

        using var connection = await _db.OpenAsync();
        var title = await connection.ExecuteScalarAsync<string?>(titleSql, new { reportId });
        if (title is null)
        {
            return false;
        }

        using var transaction = connection.BeginTransaction();
        var changed = await connection.ExecuteAsync(
            $@"UPDATE {table}
                  SET state = @dismissed, decided_at_utc = now(), decided_by_admin_id = @adminId
                WHERE id = @reportId AND state = @open",
            new { reportId, adminId, dismissed = ReportDismissed, open = ReportOpen }, transaction);
        if (changed == 0)
        {
            return false;
        }

        await LogAsync(connection, transaction, adminId, "Dismissed report", title, "No violation found", "neutral");
        transaction.Commit();
        return true;
    }

    // ------------------------------------------------------------ fees

    public async Task<List<AdminFeeDto>> GetFeesAsync()
    {
        using var connection = await _db.OpenAsync();
        return (await connection.QueryAsync<AdminFeeDto>(
            "SELECT code, label, description, amount_bdt AS amount FROM fee_settings ORDER BY sort_order")).ToList();
    }

    public async Task<(bool Ok, string Message)> SaveFeeAsync(long adminId, string code, decimal amount)
    {
        if (amount < 0)
        {
            return (false, "A fee cannot be negative.");
        }

        using var connection = await _db.OpenAsync();
        var fee = await connection.QuerySingleOrDefaultAsync<(string Label, decimal Amount)>(
            "SELECT label, amount_bdt FROM fee_settings WHERE code = @code", new { code });
        if (fee == default)
        {
            return (false, "Unknown fee.");
        }

        if (fee.Amount == amount)
        {
            return (true, "Nothing changed.");
        }

        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(
            "UPDATE fee_settings SET amount_bdt = @amount, updated_at_utc = now() WHERE code = @code",
            new { code, amount }, transaction);
        await LogAsync(connection, transaction, adminId, "Updated fee", fee.Label, $"BDT {fee.Amount:0} to BDT {amount:0}", "neutral");
        transaction.Commit();
        return (true, "Fee saved.");
    }

    // ------------------------------------------------------------ plans

    public async Task<List<AdminPlanDto>> GetPlansAsync()
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<PlanRow>(
            @"SELECT p.id, p.name, p.scope, p.posts, p.price_bdt AS price, p.valid_days, p.is_active,
                     (SELECT count(*)::int FROM plan_purchases pp WHERE pp.plan_id = p.id) AS subscribers
                FROM post_plans p
               ORDER BY p.scope, p.price_bdt, p.id");
        return rows.Select(r => new AdminPlanDto
        {
            Id = r.Id.ToString(),
            Name = r.Name,
            Scope = (ModerationScope)r.Scope,
            Posts = r.Posts,
            Price = r.Price,
            ValidDays = r.ValidDays,
            IsActive = r.IsActive,
            Subscribers = r.Subscribers
        }).ToList();
    }

    public async Task<(long? Id, string? Error)> CreatePlanAsync(long adminId, SavePlanDto dto)
    {
        var check = ValidatePlan(dto);
        if (check is not null)
        {
            return (null, check);
        }

        using var connection = await _db.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var id = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO post_plans (name, scope, posts, price_bdt, valid_days)
              VALUES (@name, @scope, @posts, @price, @days) RETURNING id",
            new { name = dto.Name.Trim(), scope = (short)dto.Scope, posts = dto.Posts, price = dto.Price, days = dto.ValidDays },
            transaction);
        await LogAsync(connection, transaction, adminId, "Created plan", $"{ScopeLabel(dto.Scope)} · {dto.Name.Trim()}",
            $"{dto.Posts} posts for BDT {dto.Price:0}", "ok");
        transaction.Commit();
        return (id, null);
    }

    public async Task<(bool Ok, string Message)> UpdatePlanAsync(long adminId, long planId, SavePlanDto dto)
    {
        var check = ValidatePlan(dto);
        if (check is not null)
        {
            return (false, check);
        }

        using var connection = await _db.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var changed = await connection.ExecuteAsync(
            @"UPDATE post_plans
                 SET name = @name, scope = @scope, posts = @posts, price_bdt = @price, valid_days = @days, updated_at_utc = now()
               WHERE id = @planId",
            new { planId, name = dto.Name.Trim(), scope = (short)dto.Scope, posts = dto.Posts, price = dto.Price, days = dto.ValidDays },
            transaction);
        if (changed == 0)
        {
            return (false, "That plan no longer exists.");
        }

        await LogAsync(connection, transaction, adminId, "Updated plan", $"{ScopeLabel(dto.Scope)} · {dto.Name.Trim()}",
            $"{dto.Posts} posts for BDT {dto.Price:0}", "neutral");
        transaction.Commit();
        return (true, "Plan saved.");
    }

    public async Task<bool> TogglePlanAsync(long adminId, long planId)
    {
        using var connection = await _db.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var row = await connection.QuerySingleOrDefaultAsync<(string Name, bool IsActive)>(
            "UPDATE post_plans SET is_active = NOT is_active, updated_at_utc = now() WHERE id = @planId RETURNING name, is_active",
            new { planId }, transaction);
        if (row == default)
        {
            return false;
        }

        await LogAsync(connection, transaction, adminId, row.IsActive ? "Enabled plan" : "Disabled plan", row.Name, null,
            row.IsActive ? "ok" : "danger");
        transaction.Commit();
        return true;
    }

    // A plan somebody already bought cannot go (plan_purchases points at it); disable it instead.
    public async Task<(bool Ok, string Message)> DeletePlanAsync(long adminId, long planId)
    {
        using var connection = await _db.OpenAsync();
        var plan = await connection.QuerySingleOrDefaultAsync<(string Name, short Scope)>(
            "SELECT name, scope FROM post_plans WHERE id = @planId", new { planId });
        if (plan == default)
        {
            return (false, "That plan no longer exists.");
        }

        var bought = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM plan_purchases WHERE plan_id = @planId)", new { planId });
        if (bought)
        {
            return (false, "People have bought this plan, so it cannot be deleted. Disable it instead.");
        }

        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync("DELETE FROM post_plans WHERE id = @planId", new { planId }, transaction);
        await LogAsync(connection, transaction, adminId, "Deleted plan",
            $"{ScopeLabel((ModerationScope)plan.Scope)} · {plan.Name}", null, "danger");
        transaction.Commit();
        return (true, "Plan deleted.");
    }

    // ------------------------------------------------------------ admin accounts

    public async Task<List<AdminAccountDto>> GetAdminsAsync()
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<AdminAccountDto>(
            @"SELECT u.id::text AS Id, u.full_name AS FullName, u.email AS Email, u.phone_number AS Phone,
                     coalesce(a.scope, 'Full access') AS Scope,
                     u.created_at_utc AS CreatedAtUtc,
                     (SELECT max(t.created_at_utc) FROM refresh_tokens t WHERE t.user_id = u.id) AS LastActiveUtc,
                     coalesce(a.is_active, true) AS IsActive
                FROM users u
                LEFT JOIN admin_accounts a ON a.user_id = u.id
               WHERE u.account_type = @admin
               ORDER BY u.created_at_utc",
            new { admin = AccountAdmin });
        return rows.ToList();
    }

    public async Task<(long? Id, string? Error)> CreateAdminAsync(long adminId, CreateAdminDto dto)
    {
        var name = (dto.FullName ?? string.Empty).Trim();
        var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        var phone = (dto.Phone ?? string.Empty).Trim();
        var scope = (dto.Scope ?? string.Empty).Trim();

        if (name.Length < 2 || email.Length == 0 || (dto.Password ?? string.Empty).Length < 8)
        {
            return (null, "Name, email and an 8+ character temporary password are needed.");
        }

        if (scope is not ("Full access" or "Verification" or "Moderation" or "Finance"))
        {
            return (null, "Pick a scope.");
        }

        using var connection = await _db.OpenAsync();

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM users WHERE email = @email)", new { email });
        if (exists)
        {
            return (null, "An account with this email already exists.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        using var transaction = connection.BeginTransaction();
        var userId = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO users (full_name, email, password_hash, phone_number, account_type)
              VALUES (@name, @email, @passwordHash, @phone, @admin) RETURNING id",
            new { name, email, passwordHash, phone, admin = AccountAdmin }, transaction);
        await connection.ExecuteAsync(
            "INSERT INTO user_roles (user_id, role_id) VALUES (@userId, @admin)",
            new { userId, admin = AccountAdmin }, transaction);
        await connection.ExecuteAsync(
            "INSERT INTO admin_accounts (user_id, scope, created_by_user_id) VALUES (@userId, @scope, @adminId)",
            new { userId, scope, adminId }, transaction);
        await LogAsync(connection, transaction, adminId, "Created admin account", name, scope, "ok");
        transaction.Commit();
        return (userId, null);
    }

    public async Task<(bool Ok, string Message)> ToggleAdminAsync(long adminId, long targetId)
    {
        if (adminId == targetId)
        {
            return (false, "You cannot suspend your own account.");
        }

        using var connection = await _db.OpenAsync();
        var name = await connection.ExecuteScalarAsync<string?>(
            "SELECT full_name FROM users WHERE id = @targetId AND account_type = @admin",
            new { targetId, admin = AccountAdmin });
        if (name is null)
        {
            return (false, "That admin no longer exists.");
        }

        using var transaction = connection.BeginTransaction();
        // An admin with no row yet is active, so the first toggle suspends.
        var isActive = await connection.ExecuteScalarAsync<bool>(
            @"INSERT INTO admin_accounts (user_id, is_active, suspended_at_utc)
              VALUES (@targetId, false, now())
              ON CONFLICT (user_id) DO UPDATE
                 SET is_active = NOT admin_accounts.is_active,
                     suspended_at_utc = CASE WHEN admin_accounts.is_active THEN now() ELSE NULL END
              RETURNING is_active",
            new { targetId }, transaction);

        if (!isActive)
        {
            await connection.ExecuteAsync(
                "UPDATE refresh_tokens SET revoked_at_utc = now() WHERE user_id = @targetId AND revoked_at_utc IS NULL",
                new { targetId }, transaction);
        }

        await LogAsync(connection, transaction, adminId, isActive ? "Enabled admin" : "Suspended admin", name, null,
            isActive ? "ok" : "danger");
        transaction.Commit();
        return (true, isActive ? "Admin restored." : "Admin suspended.");
    }

    // ------------------------------------------------------------ audit

    public async Task<List<AdminAuditEntryDto>> GetAuditAsync(int take)
    {
        using var connection = await _db.OpenAsync();
        var rows = await connection.QueryAsync<AdminAuditEntryDto>(
            @"SELECT l.id::text AS Id, u.full_name AS Admin, l.action AS Action, l.target AS Target,
                     l.note AS Note, l.kind AS Kind, l.at_utc AS AtUtc
                FROM admin_audit_log l
                JOIN users u ON u.id = l.admin_user_id
               ORDER BY l.at_utc DESC, l.id DESC
               LIMIT @take",
            new { take = Math.Clamp(take, 1, 1000) });
        return rows.ToList();
    }

    // ------------------------------------------------------------ helpers

    private static async Task LogAsync(IDbConnection connection, IDbTransaction transaction, long adminId,
        string action, string target, string? note, string kind)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await connection.ExecuteAsync(
            "INSERT INTO admin_audit_log (admin_user_id, action, target, note, kind) VALUES (@adminId, @action, @target, @note, @kind)",
            new
            {
                adminId,
                action,
                target = target.Length > 200 ? target[..200] : target,
                note = note is { Length: > 500 } ? note[..500] : note,
                kind
            },
            transaction);
    }

    private static Task<bool> HasOpenTakedownAsync(IDbConnection connection, short scope, long postId) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM post_takedowns WHERE scope = @scope AND post_id = @postId AND restored_at_utc IS NULL)",
            new { scope, postId });

    private static Task InsertTakedownAsync(IDbConnection connection, IDbTransaction transaction, short scope, long postId, long adminId, string reason) =>
        connection.ExecuteAsync(
            "INSERT INTO post_takedowns (scope, post_id, admin_user_id, reason) VALUES (@scope, @postId, @adminId, @reason)",
            new { scope, postId, adminId, reason = reason.Length > 400 ? reason[..400] : reason }, transaction);

    private static Task<int> RestoreTakedownAsync(IDbConnection connection, IDbTransaction transaction, short scope, long postId, long adminId) =>
        connection.ExecuteAsync(
            @"UPDATE post_takedowns SET restored_at_utc = now(), restored_by_admin_id = @adminId
               WHERE scope = @scope AND post_id = @postId AND restored_at_utc IS NULL",
            new { scope, postId, adminId }, transaction);

    // The report the admin clicked "strike down" from is closed with the strike.
    private static async Task ResolveLinkedReportAsync(IDbConnection connection, IDbTransaction transaction,
        string table, long adminId, string? reportId, long postId)
    {
        if (!long.TryParse(reportId, out var id))
        {
            return;
        }

        var postColumn = table == "housing_reports" ? "post_id" : "listing_id";
        await connection.ExecuteAsync(
            $@"UPDATE {table}
                  SET state = @resolved, decided_at_utc = now(), decided_by_admin_id = @adminId
                WHERE id = @id AND {postColumn} = @postId AND state = @open",
            new { id, adminId, postId, resolved = ReportResolved, open = ReportOpen }, transaction);
    }

    private static async Task<Dictionary<long, List<string>>> LoadImagesAsync(IDbConnection connection,
        string table, string column, IEnumerable<long> ids)
    {
        var result = new Dictionary<long, List<string>>();
        var keys = ids.Distinct().ToArray();
        if (keys.Length == 0)
        {
            return result;
        }

        var rows = await connection.QueryAsync<(long PostId, string ImageUrl)>(
            $"SELECT {column}, image_url FROM {table} WHERE {column} = ANY(@keys) ORDER BY {column}, sort_order",
            new { keys });

        foreach (var (postId, url) in rows)
        {
            if (!result.TryGetValue(postId, out var list))
            {
                result[postId] = list = new List<string>();
            }

            list.Add(url);
        }

        return result;
    }

    private static string Eligibility(HousingRow r)
    {
        var parts = new List<string>();
        if (r.Gender is { } gender) parts.Add(gender == 0 ? "Male" : "Female");
        if (r.Occupation is { } occupation) parts.Add(occupation == 0 ? "students only" : "working only");
        if (r.MinAge is not null && r.MaxAge is not null) parts.Add($"{r.MinAge} to {r.MaxAge}");
        else if (r.MinAge is not null) parts.Add($"{r.MinAge}+");
        else if (r.MaxAge is not null) parts.Add($"up to {r.MaxAge}");
        if (r.VerifiedOnly) parts.Add("verified accounts only");
        return parts.Count == 0 ? "Anyone" : string.Join(" · ", parts);
    }

    private static AdminReportDto ToReport(ReportRow r, ModerationScope scope) => new()
    {
        Id = r.Id.ToString(),
        Scope = scope,
        PostId = r.PostId.ToString(),
        PostTitle = r.PostTitle,
        Reason = r.Reason,
        Details = r.Details ?? string.Empty,
        ReportedBy = r.ReportedBy,
        RaisedAtUtc = r.RaisedAtUtc,
        State = r.State switch
        {
            ReportResolved => ModerationReportState.Resolved,
            ReportDismissed => ModerationReportState.Dismissed,
            _ => ModerationReportState.Open
        }
    };

    private static string? ValidatePlan(SavePlanDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 60) return "Give the plan a name.";
        if (dto.Posts < 1) return "A plan needs at least one post.";
        if (dto.Price < 0) return "Price cannot be negative.";
        if (dto.ValidDays < 1) return "A plan must be valid for at least one day.";
        return null;
    }

    private static string ScopeLabel(ModerationScope scope) =>
        scope == ModerationScope.Housing ? "Housing" : "Marketplace";

    private sealed class HousingRow
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ListingType { get; set; } = string.Empty;
        public decimal Rent { get; set; }
        public DateTime PostedAtUtc { get; set; }
        public string Area { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public int Seats { get; set; }
        public string Owner { get; set; } = string.Empty;
        public bool OwnerVerified { get; set; }
        public short? Gender { get; set; }
        public short? Occupation { get; set; }
        public short? MinAge { get; set; }
        public short? MaxAge { get; set; }
        public bool VerifiedOnly { get; set; }
        public string? RemovalReason { get; set; }
        public int ReportCount { get; set; }
    }

    private sealed class MarketRow
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Area { get; set; } = string.Empty;
        public DateTime PostedAtUtc { get; set; }
        public string Seller { get; set; } = string.Empty;
        public bool SellerVerified { get; set; }
        public string? RemovalReason { get; set; }
        public int ReportCount { get; set; }
    }

    private sealed class ReportRow
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public string PostTitle { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string ReportedBy { get; set; } = string.Empty;
        public DateTime RaisedAtUtc { get; set; }
        public short State { get; set; }
    }

    private sealed class PlanRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public short Scope { get; set; }
        public int Posts { get; set; }
        public decimal Price { get; set; }
        public int ValidDays { get; set; }
        public bool IsActive { get; set; }
        public int Subscribers { get; set; }
    }
}

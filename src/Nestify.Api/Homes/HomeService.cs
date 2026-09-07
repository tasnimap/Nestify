using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Home;

namespace Nestify.Api.Homes;

// Everything the /home page does, against the homes / home_members tables in
// User_Home.sql. The rules live here rather than in the controller: a user is in
// at most one home, a home has exactly one manager, and only a manager or a
// co-manager changes anything.
public sealed class HomeService
{
    public const short RoleManager = 1;
    public const short RoleCoManager = 2;
    public const short RoleMember = 3;

    private const short RequestPending = 1;
    private const short RequestApproved = 2;
    private const short RequestRejected = 3;
    private const short RequestCancelled = 4;

    private readonly DbConnectionFactory _db;

    public HomeService(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<HomeDto?> GetMyHomeAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        return await LoadAsync(connection, await FindHomeIdAsync(connection, userId), userId);
    }

    public async Task<(HomeDto? Home, string? Error)> CreateAsync(long userId, HomeDetailsDto dto)
    {
        var name = (dto.Name ?? string.Empty).Trim();
        var address = (dto.AddressLine ?? string.Empty).Trim();
        if (name.Length == 0 || address.Length == 0)
        {
            return (null, "House name and address are required.");
        }

        using var connection = await _db.OpenAsync();
        if (await FindHomeIdAsync(connection, userId) is not null)
        {
            return (null, "You are already in a home.");
        }

        var joinCode = await NewJoinCodeAsync(connection);

        using var transaction = connection.BeginTransaction();

        var homeId = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO homes (name, address_line, area_name, division, latitude, longitude,
                                 join_code, created_by_user_id)
              VALUES (@name, @address, @area, @division, @latitude, @longitude, @joinCode, @userId)
              RETURNING id",
            new
            {
                name,
                address,
                area = (dto.AreaName ?? string.Empty).Trim(),
                division = (dto.Division ?? string.Empty).Trim(),
                latitude = (decimal)dto.Latitude,
                longitude = (decimal)dto.Longitude,
                joinCode,
                userId
            },
            transaction);

        await connection.ExecuteAsync(
            "INSERT INTO home_members (home_id, user_id, role) VALUES (@homeId, @userId, @role)",
            new { homeId, userId, role = RoleManager },
            transaction);

        // Creating your own house answers any request you were still waiting on.
        await connection.ExecuteAsync(
            @"UPDATE home_join_requests
              SET status = @status, decided_at_utc = now()
              WHERE user_id = @userId AND status = @pending",
            new { status = RequestCancelled, userId, pending = RequestPending },
            transaction);

        transaction.Commit();
        return (await LoadAsync(connection, homeId, userId), null);
    }

    // A join code does not put anybody in the house; it files a request that the
    // manager or a co-manager decides on.
    public async Task<(bool Ok, string Message)> RequestJoinAsync(long userId, string joinCode)
    {
        var code = (joinCode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return (false, "Enter a join code.");
        }

        using var connection = await _db.OpenAsync();
        if (await FindHomeIdAsync(connection, userId) is not null)
        {
            return (false, "You are already in a home.");
        }

        if (await FindMyRequestAsync(connection, userId) is not null)
        {
            return (false, "You already have a request waiting for an answer.");
        }

        var home = await connection.QueryFirstOrDefaultAsync<HomeNameRow>(
            "SELECT id AS Id, name AS Name FROM homes WHERE upper(join_code) = upper(@code)",
            new { code });

        if (home is null)
        {
            return (false, "No home uses that join code.");
        }

        await connection.ExecuteAsync(
            "INSERT INTO home_join_requests (home_id, user_id, status) VALUES (@homeId, @userId, @status)",
            new { homeId = home.Id, userId, status = RequestPending });

        return (true, $"Request sent to {home.Name}. A manager has to approve it.");
    }

    public async Task<MyJoinRequestDto?> GetMyRequestAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        return await FindMyRequestAsync(connection, userId);
    }

    public async Task<(bool Ok, string Message)> CancelMyRequestAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var request = await FindMyRequestAsync(connection, userId);
        if (request is null)
        {
            return (false, "You have no request waiting.");
        }

        await connection.ExecuteAsync(
            "UPDATE home_join_requests SET status = @status, decided_at_utc = now() WHERE id = @id",
            new { status = RequestCancelled, id = long.Parse(request.Id) });

        return (true, "Request cancelled.");
    }

    public async Task<(bool Ok, string Message)> ApproveRequestAsync(long userId, long requestId)
    {
        using var connection = await _db.OpenAsync();
        var (me, request, error) = await LoadRequestAsync(connection, userId, requestId);
        if (error is not null)
        {
            return (false, error);
        }

        // Somebody can join another house while their request sits here, so this
        // has to be checked at approval time, not only when the request is filed.
        if (await FindHomeIdAsync(connection, request!.UserId) is not null)
        {
            await SetRequestStatusAsync(connection, requestId, RequestRejected, userId);
            return (false, $"{request.FullName} already belongs to a home.");
        }

        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(
            "INSERT INTO home_members (home_id, user_id, role) VALUES (@homeId, @userId, @role)",
            new { homeId = me!.HomeId, userId = request.UserId, role = RoleMember },
            transaction);
        await connection.ExecuteAsync(
            @"UPDATE home_join_requests
              SET status = @status, decided_at_utc = now(), decided_by_user_id = @decidedBy
              WHERE id = @id",
            new { status = RequestApproved, decidedBy = userId, id = requestId },
            transaction);
        transaction.Commit();

        return (true, $"{request.FullName} joined the house.");
    }

    public async Task<(bool Ok, string Message)> RejectRequestAsync(long userId, long requestId)
    {
        using var connection = await _db.OpenAsync();
        var (_, request, error) = await LoadRequestAsync(connection, userId, requestId);
        if (error is not null)
        {
            return (false, error);
        }

        await SetRequestStatusAsync(connection, requestId, RequestRejected, userId);
        return (true, $"Request from {request!.FullName} was rejected.");
    }

    public async Task<(bool Ok, string Message)> UpdateDetailsAsync(long userId, HomeDetailsDto dto)
    {
        var name = (dto.Name ?? string.Empty).Trim();
        var address = (dto.AddressLine ?? string.Empty).Trim();
        if (name.Length == 0 || address.Length == 0)
        {
            return (false, "House name and address are required.");
        }

        using var connection = await _db.OpenAsync();
        var me = await FindMembershipAsync(connection, userId);
        if (me is null)
        {
            return (false, "You are not in a home.");
        }

        if (me.Role == RoleMember)
        {
            return (false, "Only the manager or a co-manager can edit house details.");
        }

        await connection.ExecuteAsync(
            @"UPDATE homes
              SET name = @name, address_line = @address, area_name = @area, division = @division,
                  latitude = @latitude, longitude = @longitude, updated_at_utc = now()
              WHERE id = @homeId",
            new
            {
                name,
                address,
                area = (dto.AreaName ?? string.Empty).Trim(),
                division = (dto.Division ?? string.Empty).Trim(),
                latitude = (decimal)dto.Latitude,
                longitude = (decimal)dto.Longitude,
                homeId = me.HomeId
            });

        return (true, "House details updated.");
    }

    // Members are added by the email they registered with, so the row always
    // points at a real account.
    public async Task<(bool Ok, string Message)> AddMemberAsync(long userId, string email)
    {
        var address = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (address.Length == 0)
        {
            return (false, "Enter the email the person registered with.");
        }

        using var connection = await _db.OpenAsync();
        var me = await FindMembershipAsync(connection, userId);
        if (me is null)
        {
            return (false, "You are not in a home.");
        }

        if (me.Role == RoleMember)
        {
            return (false, "Only the manager or a co-manager can add members.");
        }

        var target = await connection.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT id AS Id, full_name AS FullName FROM users WHERE email = @address",
            new { address });

        if (target is null)
        {
            return (false, "No Nestify account uses that email.");
        }

        var existing = await FindMembershipAsync(connection, target.Id);
        if (existing is not null)
        {
            return (false, existing.HomeId == me.HomeId
                ? "That person is already a member."
                : "That person already belongs to another home.");
        }

        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(
            "INSERT INTO home_members (home_id, user_id, role) VALUES (@homeId, @userId, @role)",
            new { homeId = me.HomeId, userId = target.Id, role = RoleMember },
            transaction);

        // Somebody added straight in is in; a request they had waiting is settled
        // rather than left sitting in the queue. Approved when it was for this
        // house, cancelled when it was for another one.
        await connection.ExecuteAsync(
            @"UPDATE home_join_requests
              SET status = CASE WHEN home_id = @homeId THEN @approved ELSE @cancelled END,
                  decided_at_utc = now(),
                  decided_by_user_id = @decidedBy
              WHERE user_id = @targetId AND status = @pending",
            new
            {
                homeId = me.HomeId,
                approved = RequestApproved,
                cancelled = RequestCancelled,
                decidedBy = userId,
                targetId = target.Id,
                pending = RequestPending
            },
            transaction);
        transaction.Commit();

        return (true, $"{target.FullName} was added.");
    }

    public async Task<(bool Ok, string Message)> PromoteAsync(long userId, long memberId)
    {
        using var connection = await _db.OpenAsync();
        var (me, target, error) = await LoadPairAsync(connection, userId, memberId);
        if (error is not null)
        {
            return (false, error);
        }

        if (me!.Role == RoleMember || target!.Role != RoleMember)
        {
            return (false, "You cannot promote this member.");
        }

        await SetRoleAsync(connection, memberId, RoleCoManager);
        return (true, $"{target.FullName} is now a co-manager.");
    }

    public async Task<(bool Ok, string Message)> DemoteAsync(long userId, long memberId)
    {
        using var connection = await _db.OpenAsync();
        var (me, target, error) = await LoadPairAsync(connection, userId, memberId);
        if (error is not null)
        {
            return (false, error);
        }

        if (me!.Role != RoleManager || target!.Role != RoleCoManager)
        {
            return (false, "You cannot demote this member.");
        }

        await SetRoleAsync(connection, memberId, RoleMember);
        return (true, $"{target.FullName} is now a member.");
    }

    public async Task<(bool Ok, string Message)> RemoveMemberAsync(long userId, long memberId)
    {
        using var connection = await _db.OpenAsync();
        var (me, target, error) = await LoadPairAsync(connection, userId, memberId);
        if (error is not null)
        {
            return (false, error);
        }

        var allowed = me!.Role == RoleManager
                      || (me.Role == RoleCoManager && target!.Role == RoleMember);
        if (!allowed)
        {
            return (false, "You cannot remove this member.");
        }

        await connection.ExecuteAsync(
            "UPDATE home_members SET left_at_utc = now() WHERE id = @memberId",
            new { memberId });

        return (true, $"{target!.FullName} was removed.");
    }

    // The manager steps down to co-manager as the target steps up, in one
    // transaction, so the home is never left without a manager and the
    // single-manager index is never broken half way through.
    public async Task<(bool Ok, string Message)> TransferManagerAsync(long userId, long memberId)
    {
        using var connection = await _db.OpenAsync();
        var (me, target, error) = await LoadPairAsync(connection, userId, memberId);
        if (error is not null)
        {
            return (false, error);
        }

        if (me!.Role != RoleManager)
        {
            return (false, "Only the manager can transfer the manager role.");
        }

        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(
            "UPDATE home_members SET role = @role WHERE id = @id",
            new { role = RoleCoManager, id = me.Id },
            transaction);
        await connection.ExecuteAsync(
            "UPDATE home_members SET role = @role WHERE id = @id",
            new { role = RoleManager, id = memberId },
            transaction);
        transaction.Commit();

        return (true, $"{target!.FullName} is now the manager.");
    }

    public async Task<(bool Ok, string Message)> LeaveAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var me = await FindMembershipAsync(connection, userId);
        if (me is null)
        {
            return (false, "You are not in a home.");
        }

        if (me.Role == RoleManager)
        {
            return (false, "Transfer the manager role before leaving.");
        }

        await connection.ExecuteAsync(
            "UPDATE home_members SET left_at_utc = now() WHERE id = @id",
            new { id = me.Id });

        return (true, "You left the home.");
    }

    private static async Task<long?> FindHomeIdAsync(IDbConnection connection, long userId) =>
        await connection.ExecuteScalarAsync<long?>(
            "SELECT home_id FROM home_members WHERE user_id = @userId AND left_at_utc IS NULL",
            new { userId });

    private static async Task<Membership?> FindMembershipAsync(IDbConnection connection, long userId) =>
        await connection.QueryFirstOrDefaultAsync<Membership>(
            @"SELECT hm.id AS Id, hm.home_id AS HomeId, hm.user_id AS UserId, hm.role AS Role,
                     u.full_name AS FullName
              FROM home_members hm
              JOIN users u ON u.id = hm.user_id
              WHERE hm.user_id = @userId AND hm.left_at_utc IS NULL",
            new { userId });

    // Both sides of an action on somebody else: the caller, and the target read by
    // home_members.id. The target has to be an active member of the caller's home.
    private static async Task<(Membership? Me, Membership? Target, string? Error)> LoadPairAsync(
        IDbConnection connection, long userId, long memberId)
    {
        var me = await FindMembershipAsync(connection, userId);
        if (me is null)
        {
            return (null, null, "You are not in a home.");
        }

        var target = await connection.QueryFirstOrDefaultAsync<Membership>(
            @"SELECT hm.id AS Id, hm.home_id AS HomeId, hm.user_id AS UserId, hm.role AS Role,
                     u.full_name AS FullName
              FROM home_members hm
              JOIN users u ON u.id = hm.user_id
              WHERE hm.id = @memberId AND hm.home_id = @homeId AND hm.left_at_utc IS NULL",
            new { memberId, homeId = me.HomeId });

        if (target is null)
        {
            return (null, null, "Member not found.");
        }

        if (target.UserId == userId)
        {
            return (null, null, "You cannot do that to yourself.");
        }

        return (me, target, null);
    }

    private static async Task<MyJoinRequestDto?> FindMyRequestAsync(IDbConnection connection, long userId) =>
        await connection.QueryFirstOrDefaultAsync<MyJoinRequestDto>(
            @"SELECT r.id::text AS Id, h.name AS HomeName, r.requested_at_utc AS RequestedAtUtc
              FROM home_join_requests r
              JOIN homes h ON h.id = r.home_id
              WHERE r.user_id = @userId AND r.status = @status",
            new { userId, status = RequestPending });

    // A pending request together with the caller, who has to be a manager or a
    // co-manager of the home the request was sent to.
    private static async Task<(Membership? Me, JoinRequestRow? Request, string? Error)> LoadRequestAsync(
        IDbConnection connection, long userId, long requestId)
    {
        var me = await FindMembershipAsync(connection, userId);
        if (me is null)
        {
            return (null, null, "You are not in a home.");
        }

        if (me.Role == RoleMember)
        {
            return (null, null, "Only the manager or a co-manager can answer join requests.");
        }

        var request = await connection.QueryFirstOrDefaultAsync<JoinRequestRow>(
            @"SELECT r.id AS Id, r.user_id AS UserId, u.full_name AS FullName
              FROM home_join_requests r
              JOIN users u ON u.id = r.user_id
              WHERE r.id = @requestId AND r.home_id = @homeId AND r.status = @status",
            new { requestId, homeId = me.HomeId, status = RequestPending });

        return request is null
            ? (null, null, "That request is no longer waiting.")
            : (me, request, null);
    }

    private static Task SetRequestStatusAsync(IDbConnection connection, long requestId, short status, long decidedBy) =>
        connection.ExecuteAsync(
            @"UPDATE home_join_requests
              SET status = @status, decided_at_utc = now(), decided_by_user_id = @decidedBy
              WHERE id = @requestId",
            new { status, decidedBy, requestId });

    private static Task SetRoleAsync(IDbConnection connection, long memberId, short role) =>
        connection.ExecuteAsync(
            "UPDATE home_members SET role = @role WHERE id = @memberId",
            new { role, memberId });

    private static async Task<HomeDto?> LoadAsync(IDbConnection connection, long? homeId, long userId)
    {
        if (homeId is null)
        {
            return null;
        }

        var home = await connection.QueryFirstOrDefaultAsync<HomeDto>(
            @"SELECT id::text AS Id, name AS Name, address_line AS AddressLine, area_name AS AreaName,
                     division AS Division, latitude AS Latitude, longitude AS Longitude,
                     join_code AS JoinCode, created_at_utc AS CreatedAtUtc
              FROM homes
              WHERE id = @homeId",
            new { homeId });

        if (home is null)
        {
            return null;
        }

        var members = await connection.QueryAsync<HomeMemberDto>(
            @"SELECT hm.id::text AS Id, hm.user_id::text AS UserId, u.full_name AS Name, u.email AS Email,
                     hm.role AS Role, hm.joined_at_utc AS JoinedAtUtc,
                     (hm.user_id = @userId) AS IsMe
              FROM home_members hm
              JOIN users u ON u.id = hm.user_id
              WHERE hm.home_id = @homeId AND hm.left_at_utc IS NULL
              ORDER BY hm.role, hm.joined_at_utc",
            new { homeId, userId });

        home.Members = members.ToList();

        // Plain members never see the queue, so it is not sent to them at all.
        var myRole = home.Members.FirstOrDefault(m => m.IsMe)?.Role ?? RoleMember;
        if (myRole != RoleMember)
        {
            var requests = await connection.QueryAsync<HomeJoinRequestDto>(
                @"SELECT r.id::text AS Id, u.full_name AS Name, u.email AS Email,
                         r.requested_at_utc AS RequestedAtUtc
                  FROM home_join_requests r
                  JOIN users u ON u.id = r.user_id
                  WHERE r.home_id = @homeId AND r.status = @status
                  ORDER BY r.requested_at_utc",
                new { homeId, status = RequestPending });

            home.PendingRequests = requests.ToList();
        }

        return home;
    }

    // Codes read like NST-4821. A handful of tries is plenty against the unique
    // index; the fallback only ever runs if every one of them collided.
    private static async Task<string> NewJoinCodeAsync(IDbConnection connection)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var code = "NST-" + Random.Shared.Next(1000, 10000);
            var taken = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM homes WHERE join_code = @code)",
                new { code });

            if (!taken)
            {
                return code;
            }
        }

        return "NST-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }

    private sealed class Membership
    {
        public long Id { get; set; }
        public long HomeId { get; set; }
        public long UserId { get; set; }
        public short Role { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    private sealed class HomeNameRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class JoinRequestRow
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    private sealed class UserRow
    {
        public long Id { get; set; }
        public string FullName { get; set; } = string.Empty;
    }
}

using System.Data;
using Dapper;

namespace Nestify.Api.Housing;

// What happens to housing posts and bookings when somebody moves into a home.
// Called by HomeService inside its own transaction whenever a member row is
// added (approved join request or add-by-email).
public static class HousingHooks
{
    private const short PostActive = 1;
    private const short PostFilled = 3;

    private const short BookingPending = 1;
    private const short BookingAccepted = 2;
    private const short BookingJoined = 5;
    private const short BookingHouseFull = 6;

    public static async Task AfterMemberJoinedAsync(IDbConnection connection, IDbTransaction transaction, long homeId, long userId)
    {
        // The newcomer's own open bookings on this home's posts are done: they got in.
        await connection.ExecuteAsync(
            @"UPDATE housing_bookings b
              SET status = @joined, decided_at_utc = COALESCE(b.decided_at_utc, now())
              FROM housing_posts p
              WHERE p.id = b.post_id AND p.home_id = @homeId
                AND b.requester_user_id = @userId AND b.status IN (@pending, @accepted)",
            new { joined = BookingJoined, homeId, userId, pending = BookingPending, accepted = BookingAccepted },
            transaction);

        await SynchronizeHomeCapacityAsync(connection, transaction, homeId);
    }

    // Called after a member leaves, is removed, or a manager changes capacity.
    // Filled listings become available again as soon as the home has a free seat.
    public static Task AfterCapacityChangedAsync(IDbConnection connection, IDbTransaction transaction, long homeId) =>
        SynchronizeHomeCapacityAsync(connection, transaction, homeId);

    private static async Task SynchronizeHomeCapacityAsync(IDbConnection connection, IDbTransaction transaction, long homeId)
    {
        var full = await connection.ExecuteScalarAsync<bool>(
            @"SELECT (SELECT count(*) FROM home_members WHERE home_id = @homeId AND left_at_utc IS NULL)
                     >= COALESCE((SELECT max_occupants FROM home_capacity WHERE home_id = @homeId), 4)",
            new { homeId },
            transaction);

        if (!full)
        {
            await connection.ExecuteAsync(
                @"UPDATE housing_posts
                  SET status = @active, closed_at_utc = NULL, updated_at_utc = now()
                  WHERE home_id = @homeId AND status = @filled",
                new { homeId, active = PostActive, filled = PostFilled },
                transaction);
            return;
        }

        // Living here now == max occupants: the posts are done, and everyone
        // else still waiting on them is told the house is full.
        await connection.ExecuteAsync(
            @"UPDATE housing_bookings b
              SET status = @houseFull, decided_at_utc = now()
              FROM housing_posts p
              WHERE p.id = b.post_id AND p.home_id = @homeId AND b.status IN (@pending, @accepted)",
            new { houseFull = BookingHouseFull, homeId, pending = BookingPending, accepted = BookingAccepted },
            transaction);

        await connection.ExecuteAsync(
            @"UPDATE housing_posts
              SET status = @filled, closed_at_utc = now(), updated_at_utc = now()
              WHERE home_id = @homeId AND status = @active",
            new { filled = PostFilled, homeId, active = PostActive },
            transaction);
    }
}

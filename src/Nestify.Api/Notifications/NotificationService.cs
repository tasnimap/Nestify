using System.Data;
using Dapper;
using Nestify.Api.Data;
using Nestify.Shared.Dtos.Notifications;

namespace Nestify.Api.Notifications;

public sealed class NotificationService
{
    private readonly DbConnectionFactory _db;

    public NotificationService(DbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<NotificationFeedDto> GetForUserAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        var unreadCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM public.notifications WHERE recipient_user_id = @userId AND is_read = false",
            new { userId });

        var notifications = await connection.QueryAsync<NotificationDto>("""
            SELECT id,
                   type AS Type,
                   title AS Title,
                   body AS Body,
                   link_path AS LinkPath,
                   source_type AS SourceType,
                   source_id AS SourceId,
                   is_read AS IsRead,
                   created_at_utc AS CreatedAtUtc
            FROM public.notifications
            WHERE recipient_user_id = @userId
            ORDER BY created_at_utc DESC, id DESC
            """, new { userId });

        return new NotificationFeedDto
        {
            UnreadCount = unreadCount,
            Notifications = notifications.ToList()
        };
    }

    public async Task<bool> MarkReadAsync(long userId, long notificationId)
    {
        using var connection = await _db.OpenAsync();
        var changed = await connection.ExecuteAsync("""
            UPDATE public.notifications
            SET is_read = true
            WHERE id = @notificationId AND recipient_user_id = @userId AND is_read = false
            """, new { userId, notificationId });
        if (changed > 0)
        {
            return true;
        }

        return await connection.ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM public.notifications
                WHERE id = @notificationId AND recipient_user_id = @userId
            )
            """, new { userId, notificationId });
    }

    public async Task MarkAllReadAsync(long userId)
    {
        using var connection = await _db.OpenAsync();
        await connection.ExecuteAsync("""
            UPDATE public.notifications
            SET is_read = true
            WHERE recipient_user_id = @userId AND is_read = false
            """, new { userId });
    }

    public Task CreateAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long recipientUserId,
        NotificationType type,
        string title,
        string body,
        NotificationSourceType sourceType,
        long sourceId,
        string? linkPath)
    {
        return connection.ExecuteAsync("""
            INSERT INTO public.notifications
                (recipient_user_id, type, title, body, link_path, source_type, source_id)
            VALUES
                (@recipientUserId, @type, @title, @body, @linkPath, @sourceType, @sourceId)
            ON CONFLICT (recipient_user_id, source_type, source_id, type) DO NOTHING
            """, new
        {
            recipientUserId,
            type = (short)type,
            title,
            body,
            linkPath,
            sourceType = (short)sourceType,
            sourceId
        }, transaction);
    }
}

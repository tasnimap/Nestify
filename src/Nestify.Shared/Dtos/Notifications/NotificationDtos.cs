namespace Nestify.Shared.Dtos.Notifications;

// Values 1-8 retain the meanings already assigned by the helper workflow.
// The database accepts type 9, but this repository does not assign it.
public enum NotificationType : short
{
    HelperRequestReceived = 1,
    PendingRequestClosed = 2,
    EngagementAccepted = 3,
    EngagementDeclined = 4,
    HelperCompletionMarked = 5,
    EngagementReleased = 6,
    HelperReviewSubmitted = 7,
    ReviewReplyReceived = 8
}

// No source-type registry existed in the repository. These values distinguish
// engagement IDs from review IDs for deduplication and future navigation.
public enum NotificationSourceType : short
{
    DomesticHelperEngagement = 1,
    DomesticHelperReview = 2
}

public sealed class NotificationDto
{
    public long Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? LinkPath { get; set; }
    public NotificationSourceType SourceType { get; set; }
    public long SourceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class NotificationFeedDto
{
    public List<NotificationDto> Notifications { get; set; } = new();
    public int UnreadCount { get; set; }
}

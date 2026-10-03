namespace Nestify.Shared.Dtos.Notifications;

// Values 1-8 retain the meanings already assigned by the helper workflow.
// Type 9 is the existing general-update type used by other application areas.
public enum NotificationType : short
{
    HelperRequestReceived = 1,
    PendingRequestClosed = 2,
    EngagementAccepted = 3,
    EngagementDeclined = 4,
    HelperCompletionMarked = 5,
    EngagementReleased = 6,
    HelperReviewSubmitted = 7,
    ReviewReplyReceived = 8,
    GeneralUpdate = 9
}

// Source type scopes the existing (recipient, source type, source id, type)
// deduplication key to the domain event that produced a notification.
public enum NotificationSourceType : short
{
    DomesticHelperEngagement = 1,
    DomesticHelperReview = 2,
    HomeJoinRequest = 3,
    HomeMembership = 4,
    HomeRoleChange = 5,
    SettlementMember = 6,
    SettlementBill = 7,
    SettlementPayment = 8,
    SettlementFinalization = 9,
    MarketplaceInterest = 10,
    MarketplaceListing = 11,
    AdminAccount = 12,
    AccountVerification = 13,
    HelperAvailability = 14
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

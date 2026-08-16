namespace PEMS.Application.Notifications.Common;

public sealed class NotificationDto
{
    public ulong NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Category { get; set; } = "GENERAL";
    public string Priority { get; set; } = "NORMAL";
    public bool IsActionRequired { get; set; }
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public ulong? VisitRequestId { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public ulong? CampusId { get; set; }
    public string? ActionType { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgoText { get; set; } = string.Empty;

    /// <summary>Raw `{"messageKey":"...","params":{...}}` JSON, present only for the notification
    /// templates in <see cref="NotificationMessageKeys"/>. Null for every other notification
    /// (including all historical rows) — the client falls back to Title/Message when null.</summary>
    public string? MetadataJson { get; set; }

    // Backward-compatible fields the existing frontend already consumes.
    // TargetUrl is a direct alias of the stored ActionUrl (static, written at creation time).
    public string? TargetUrl { get; set; }
    public bool CanOpen { get; set; }
    public string? DisabledReason { get; set; }
}

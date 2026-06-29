namespace PEMS.Application.Notifications.Common;

public sealed class NotificationDto
{
    public ulong NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgoText { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public bool CanOpen { get; set; }
    public string? DisabledReason { get; set; }
}

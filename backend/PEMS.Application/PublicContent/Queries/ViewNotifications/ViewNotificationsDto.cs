namespace PEMS.Application.PublicContent.Queries.ViewNotifications;

public sealed class NotificationItemDto
{
    public ulong   NotificationId   { get; init; }
    public string  NotificationType { get; init; } = string.Empty;
    public string  Title            { get; init; } = string.Empty;
    public string? Message          { get; init; }
    public string? RelatedType      { get; init; }
    public ulong?  RelatedId        { get; init; }
    public bool    IsRead           { get; init; }
    public DateTime CreatedAt       { get; init; }
}

public sealed class ViewNotificationsDto
{
    public IReadOnlyList<NotificationItemDto> Items       { get; init; } = Array.Empty<NotificationItemDto>();
    public int                                UnreadCount { get; init; }
}

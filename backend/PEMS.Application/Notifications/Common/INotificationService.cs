namespace PEMS.Application.Notifications.Common;

public interface INotificationService
{
    Task CreateAsync(
        ulong recipientUserId,
        string title,
        string? message,
        string notificationType,
        string? relatedType,
        ulong? relatedId,
        CancellationToken cancellationToken);

    Task CreateManyAsync(
        IEnumerable<CreateNotificationItem> items,
        CancellationToken cancellationToken);
}

public sealed record CreateNotificationItem(
    ulong RecipientUserId,
    string Title,
    string? Message,
    string NotificationType,
    string? RelatedType,
    ulong? RelatedId);

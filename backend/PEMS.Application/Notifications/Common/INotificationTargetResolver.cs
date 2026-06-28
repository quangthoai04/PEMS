namespace PEMS.Application.Notifications.Common;

public interface INotificationTargetResolver
{
    Task<(string? targetUrl, bool canOpen, string? disabledReason)> ResolveTargetAsync(
        ulong currentUserId, 
        string notificationType, 
        string? relatedType, 
        ulong? relatedId, 
        CancellationToken cancellationToken);
}

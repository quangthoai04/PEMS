using PEMS.Domain.Enums;

namespace PEMS.Application.Notifications.Common;

public interface INotificationService
{
    // Legacy (v9) — unchanged signature, still used by non-migrated call sites.
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

    // v10 — full-featured single create.
    Task CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken);

    // v10 — full-featured batch create.
    Task CreateManyAsync(
        IEnumerable<CreateNotificationRequest> requests,
        CancellationToken cancellationToken);
}

public sealed record CreateNotificationItem(
    ulong RecipientUserId,
    string Title,
    string? Message,
    string NotificationType,
    string? RelatedType,
    ulong? RelatedId);

/// <summary>Full-featured notification creation request covering every v10 `notifications`
/// column. Only RecipientUserId/Title/NotificationType are mandatory - everything else defaults
/// to the same behavior the legacy CreateAsync/CreateManyAsync had.</summary>
public sealed record CreateNotificationRequest(
    ulong RecipientUserId,
    string Title,
    string? Message,
    string NotificationType,
    string? RelatedType = null,
    ulong? RelatedId = null,
    ulong? ActorUserId = null,
    string Category = "GENERAL",
    NotificationPriority Priority = NotificationPriority.NORMAL,
    bool IsActionRequired = false,
    ulong? VisitRequestId = null,
    ulong? VisitInstanceId = null,
    ulong? CampusId = null,
    string? ActionType = null,
    string? ActionUrl = null,
    string? MetadataJson = null,
    string? DedupeKey = null);

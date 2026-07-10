using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Notifications;

namespace PEMS.Application.Notifications.Common;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;

    public NotificationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(
        ulong recipientUserId,
        string title,
        string? message,
        string notificationType,
        string? relatedType,
        ulong? relatedId,
        CancellationToken cancellationToken)
        => CreateAsync(
            new CreateNotificationRequest(recipientUserId, title, message, notificationType, relatedType, relatedId),
            cancellationToken);

    public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken cancellationToken)
        => CreateManyAsync(
            items.Select(i => new CreateNotificationRequest(
                i.RecipientUserId, i.Title, i.Message, i.NotificationType, i.RelatedType, i.RelatedId)),
            cancellationToken);

    public async Task CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        await CreateManyAsync(new[] { request }, cancellationToken);
    }

    public async Task CreateManyAsync(
        IEnumerable<CreateNotificationRequest> requests,
        CancellationToken cancellationToken)
    {
        var validItems = requests
            .DistinctBy(i => (i.RecipientUserId, i.NotificationType, i.RelatedType, i.RelatedId, i.DedupeKey))
            .ToList();

        if (validItems.Count == 0) return;

        var recipientIds = validItems.Select(i => i.RecipientUserId).Distinct().ToList();
        var activeUsers = await _context.Users
            .Where(u => recipientIds.Contains(u.UserId) && u.Status == "ACTIVE")
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);
        var activeUserSet = activeUsers.ToHashSet();

        // Proactive dedupe-key check: (recipient_user_id, dedupe_key) is UNIQUE in the DB, so
        // reminders/idempotent creates that already exist are skipped cleanly here instead of
        // relying on a thrown constraint-violation exception.
        var dedupeCandidates = validItems.Where(i => i.DedupeKey != null).Select(i => i.RecipientUserId).Distinct().ToList();
        var existingDedupeKeys = dedupeCandidates.Count == 0
            ? new HashSet<(ulong, string)>()
            : (await _context.Notifications
                .Where(n => n.DedupeKey != null && dedupeCandidates.Contains(n.RecipientUserId))
                .Select(n => new { n.RecipientUserId, n.DedupeKey })
                .ToListAsync(cancellationToken))
                .Select(x => (x.RecipientUserId, x.DedupeKey!))
                .ToHashSet();

        var notifications = new List<Notification>();

        foreach (var item in validItems)
        {
            if (!activeUserSet.Contains(item.RecipientUserId))
                continue;
            if (item.DedupeKey != null && existingDedupeKeys.Contains((item.RecipientUserId, item.DedupeKey)))
                continue;

            notifications.Add(new Notification
            {
                RecipientUserId = item.RecipientUserId,
                ActorUserId = item.ActorUserId,
                Title = item.Title.Trim().Length > 255 ? item.Title.Trim().Substring(0, 255) : item.Title.Trim(),
                Message = item.Message,
                NotificationType = item.NotificationType,
                Category = item.Category,
                Priority = item.Priority,
                IsActionRequired = item.IsActionRequired,
                RelatedType = item.RelatedType,
                RelatedId = item.RelatedId,
                VisitRequestId = item.VisitRequestId,
                VisitInstanceId = item.VisitInstanceId,
                CampusId = item.CampusId,
                ActionType = item.ActionType,
                ActionUrl = item.ActionUrl,
                MetadataJson = item.MetadataJson,
                DedupeKey = item.DedupeKey,
                IsRead = false,
                ReadAt = null,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (notifications.Count > 0)
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

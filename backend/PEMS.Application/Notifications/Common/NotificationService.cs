using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Notifications;

using PEMS.Application.Common;
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
                CreatedAt = VietnamTime.Now()
            });
        }

        if (notifications.Count == 0) return;

        _context.Notifications.AddRange(notifications);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        // DB-TXN-010: the proactive check above (existingDedupeKeys) is a plain read with nothing
        // holding the gap shut — a second caller racing the exact same dedupe key (e.g. two overlapping
        // reminder-job ticks, or two requests hitting the same idempotent notify path) can pass that
        // check too and then lose the unique-constraint race at SaveChangesAsync. That loss must land
        // as a graceful no-op, not an unhandled exception bubbling out of what is usually a side effect
        // inside a larger caller's own business transaction (e.g. approving a campus instance). But this
        // is a BATCH insert of possibly several unrelated notifications, so a bare
        // catch (DbUpdateException) would just as happily swallow a real problem — an FK violation on a
        // bad ActorUserId/VisitRequestId, a truncation error, anything. Only notifications with a
        // DedupeKey can possibly hit uq_notifications_recipient_dedupe (MySQL never treats two NULLs as
        // equal in a unique index), so a batch with none can't be this race at all. For a batch that
        // does, the exact cause is confirmed by re-reading current DB state rather than parsing the
        // driver's exception type/message: whatever pair now already exists WAS the collision;
        // whatever doesn't still needs to be saved and was never at fault.
        catch (DbUpdateException) when (notifications.Any(n => n.DedupeKey != null))
        {
            foreach (var n in notifications)
                _context.Notifications.Remove(n); // Added-but-unsaved entity -> detaches, no DELETE issued.

            var recheckCandidates = notifications
                .Where(n => n.DedupeKey != null)
                .Select(n => n.RecipientUserId)
                .Distinct()
                .ToList();
            var stillTaken = (await _context.Notifications
                    .Where(n => n.DedupeKey != null && recheckCandidates.Contains(n.RecipientUserId))
                    .Select(n => new { n.RecipientUserId, n.DedupeKey })
                    .ToListAsync(cancellationToken))
                .Select(x => (x.RecipientUserId, x.DedupeKey!))
                .ToHashSet();

            var survivors = notifications
                .Where(n => n.DedupeKey == null || !stillTaken.Contains((n.RecipientUserId, n.DedupeKey)))
                .ToList();

            // Nothing in the batch was actually a dedupe collision on re-check (every pair we tried to
            // insert is still free) -> the failure was something else entirely. Don't hide it.
            if (survivors.Count == notifications.Count)
                throw;

            if (survivors.Count > 0)
            {
                _context.Notifications.AddRange(survivors);
                await _context.SaveChangesAsync(cancellationToken);
            }
            // else: every notification in the batch turned out to be a duplicate someone else just
            // committed - the graceful outcome this whole path exists for.
        }
    }
}

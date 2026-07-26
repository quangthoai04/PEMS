using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Domain.Constants;

// Both namespaces declare this name; the notifications written by the visit paths use the
// Application one, so that is what has to be matched here.
using NotificationTypes = PEMS.Application.Notifications.Common.NotificationTypes;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// Builds the per-row "something changed here" summary for the management list.
///
/// The source is the NOTIFICATIONS the caller already received. That table already records exactly
/// what this needs — recipient, request, instance, event type, read state, time — and it is already
/// written by every mutation path, so a second read-receipt table would be a parallel truth to keep
/// in step with it for no gain.
///
/// Reading it per row would mean one query per row. Everything here is batched: one query for the
/// whole page, grouped in memory.
/// </summary>
public static class VisitChangeSummaryBuilder
{
    /// <summary>Notification types that represent a CHANGE worth flagging on the list.</summary>
    private static readonly HashSet<string> ChangeTypes = new(StringComparer.Ordinal)
    {
        NotificationTypes.VisitRequestSubmitted,
        NotificationTypes.VisitStatusChanged,
        NotificationTypes.HostAssigned,
        NotificationTypes.VisitRequestApproved,
        NotificationTypes.VisitRequestRejected,
    };

    /// <summary>
    /// One batched pass over the caller's unread notifications for the given requests, plus the
    /// pending-amendment counts for the campuses they can see.
    /// </summary>
    /// <param name="visibleInstanceIdsByRequest">
    /// The campuses this caller may see, per request. A campus missing from here contributes nothing:
    /// its notifications are skipped and it produces no indicator, so an unread count can never be
    /// used to infer that a hidden campus exists.
    /// </param>
    public static async Task<Dictionary<ulong, VisitListChangeSummaryDto>> BuildAsync(
        IApplicationDbContext db,
        ulong userId,
        IReadOnlyCollection<ulong> visitRequestIds,
        IReadOnlyDictionary<ulong, HashSet<ulong>> visibleInstanceIdsByRequest,
        IReadOnlyDictionary<ulong, string> campusNameByInstance,
        CancellationToken ct)
    {
        var result = new Dictionary<ulong, VisitListChangeSummaryDto>();
        if (visitRequestIds.Count == 0) return result;

        var unread = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == userId
                        && n.VisitRequestId != null
                        && visitRequestIds.Contains(n.VisitRequestId.Value)
                        && !n.IsRead
                        && n.ArchivedAt == null)
            .Select(n => new
            {
                RequestId = n.VisitRequestId!.Value,
                n.VisitInstanceId,
                n.NotificationType,
                n.IsActionRequired,
                n.CreatedAt,
            })
            .ToListAsync(ct);

        var pendingAmendments = await db.VisitInstanceAmendments.AsNoTracking()
            .Where(a => visitRequestIds.Contains(a.VisitRequestId)
                        && a.Status == AmendmentStatuses.PendingApproval)
            .Select(a => new { a.VisitRequestId, a.VisitInstanceId })
            .ToListAsync(ct);

        foreach (var requestId in visitRequestIds)
        {
            var visible = visibleInstanceIdsByRequest.TryGetValue(requestId, out var set)
                ? set : new HashSet<ulong>();

            // A request-level notification (no instance) is in scope for anyone who can see the row.
            var rows = unread
                .Where(n => n.RequestId == requestId
                            && ChangeTypes.Contains(n.NotificationType)
                            && (n.VisitInstanceId is null || visible.Contains(n.VisitInstanceId.Value)))
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            var amendmentCount = pendingAmendments
                .Count(a => a.VisitRequestId == requestId && visible.Contains(a.VisitInstanceId));

            if (rows.Count == 0 && amendmentCount == 0) continue;

            var latest = rows.FirstOrDefault();
            var indicators = rows
                .Where(n => n.VisitInstanceId is not null)
                // One indicator per campus — the most recent thing that happened there. Ten edits to
                // one campus is still "campus X changed", not ten badges.
                .GroupBy(n => n.VisitInstanceId!.Value)
                .Select(g =>
                {
                    var newest = g.OrderByDescending(n => n.CreatedAt).First();
                    return new VisitListCampusIndicatorDto
                    {
                        VisitInstanceId = g.Key,
                        CampusName = campusNameByInstance.TryGetValue(g.Key, out var name) ? name : null,
                        EventCode = EventCodeOf(newest.NotificationType),
                        OccurredAt = newest.CreatedAt,
                        RequiresAction = g.Any(n => n.IsActionRequired),
                    };
                })
                .OrderByDescending(i => i.OccurredAt)
                .ToList();

            result[requestId] = new VisitListChangeSummaryDto
            {
                HasUnreadChanges = rows.Count > 0,
                UnreadChangeCount = rows.Count,
                LatestEventCode = latest is null ? null : EventCodeOf(latest.NotificationType),
                LatestChangedAt = latest?.CreatedAt,
                PendingAmendmentCount = amendmentCount,
                // "Needs me" is narrower than "is unread": a Host being told the notes changed has
                // nothing to do about it, and a badge that says otherwise trains people to ignore it.
                RequiresViewerAction = rows.Any(n => n.IsActionRequired) || amendmentCount > 0,
                CampusIndicators = indicators,
            };
        }

        return result;
    }

    /// <summary>
    /// Notification type → list event code. The list speaks the same vocabulary as the timeline so
    /// the badge and the history entry cannot describe the same event differently.
    /// </summary>
    private static string EventCodeOf(string notificationType) => notificationType switch
    {
        NotificationTypes.HostAssigned => VisitListEventCodes.HostChanged,
        NotificationTypes.VisitRequestApproved => VisitListEventCodes.Decided,
        NotificationTypes.VisitRequestRejected => VisitListEventCodes.Decided,
        NotificationTypes.VisitStatusChanged => VisitListEventCodes.StatusChanged,
        _ => VisitListEventCodes.ContentUpdated,
    };
}

/// <summary>The badge vocabulary. A value the client has not been taught renders as a neutral one.</summary>
public static class VisitListEventCodes
{
    public const string ContentUpdated = "CONTENT_UPDATED";
    public const string StatusChanged = "STATUS_CHANGED";
    public const string HostChanged = "HOST_CHANGED";
    public const string Decided = "DECIDED";
}

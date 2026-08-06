using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// Post-commit announcements for the confirmation gate.
///
/// Nothing here may fail the write that preceded it: the campus relation is already committed, and a
/// notification problem must not turn a successful confirmation into an error the user retries.
/// </summary>
internal static class OperationalContactNotifier
{
    /// <summary>
    /// The request just became visible to Staff Leaders: tell each campus's leader about THEIR campus
    /// only.
    ///
    /// Deduped on <c>APPROVAL_READY:{requestId}:{instanceId}:{gateRevision}</c>, which the database
    /// enforces as unique per recipient. That is what makes two racing final confirmations announce a
    /// campus once rather than twice — and what lets a later re-opening of the gate (a contact was
    /// replaced after confirming) announce it again, because the revision has moved on.
    /// </summary>
    public static async Task AnnounceApprovalReadyAsync(
        IApplicationDbContext db,
        INotificationService notifications,
        ILogger logger,
        ulong visitRequestId,
        uint gateRevision,
        CancellationToken cancellationToken)
    {
        try
        {
            var head = await db.VisitRequests.AsNoTracking()
                .Where(v => v.VisitRequestId == visitRequestId)
                .Select(v => new { v.RequestCode })
                .FirstOrDefaultAsync(cancellationToken);
            if (head is null)
                return;

            // Only campuses actually awaiting a decision. A campus that was cancelled, rejected or
            // already decided is not somebody's pending task, and raising one would be a fake job.
            var targets = await db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitRequestId == visitRequestId
                            && c.Status == VisitInstanceStatuses.WaitingRequestApproval
                            && c.CoordinatorUserId != null)
                .Select(c => new
                {
                    c.VisitInstanceId,
                    c.CampusId,
                    CoordinatorUserId = c.CoordinatorUserId!.Value,
                    DelegationName = c.FormDetail!.DelegationName,
                })
                .ToListAsync(cancellationToken);
            if (targets.Count == 0)
                return;

            var items = targets.Select(t => new CreateNotificationRequest(
                RecipientUserId: t.CoordinatorUserId,
                Title: "Có yêu cầu tiếp khách chờ duyệt",
                // Each leader is told about their OWN campus's delegation, so a mixed request never
                // shows one campus's content to another campus's reviewer.
                Message: $"{t.DelegationName} đã đủ đầu mối vận hành và đang chờ bạn duyệt tại cơ sở của mình.",
                NotificationType: Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                RelatedType: Notifications.Common.NotificationRelatedTypes.VisitRequest,
                RelatedId: visitRequestId,
                Category: NotificationCategories.Visit,
                IsActionRequired: true,
                VisitRequestId: visitRequestId,
                VisitInstanceId: t.VisitInstanceId,
                CampusId: t.CampusId,
                ActionType: NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit?visitRequestId={visitRequestId}",
                DedupeKey: $"APPROVAL_READY:{visitRequestId}:{t.VisitInstanceId}:{gateRevision}"))
                .ToList();

            await notifications.CreateManyAsync(items, cancellationToken);
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex,
                "approval-ready announcement failed for visit request {VisitRequestId} at gate revision {GateRevision}",
                visitRequestId, gateRevision);
        }
    }

    /// <summary>
    /// A campus changed hands. The person who lost it is told plainly that only the campus relation
    /// moved and their account is untouched; the registrant is told the handover completed.
    /// </summary>
    public static async Task AnnounceTransferAppliedAsync(
        INotificationService notifications,
        ILogger logger,
        ulong visitRequestId,
        ulong visitInstanceId,
        string requestCode,
        string campusLabel,
        ulong? previousContactUserId,
        ulong? registrantUserId,
        ulong newContactUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = new List<CreateNotificationRequest>();

            void Add(ulong recipient, string title, string message) =>
                items.Add(new CreateNotificationRequest(
                    RecipientUserId: recipient,
                    Title: title,
                    Message: message,
                    NotificationType: Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visitRequestId,
                    ActorUserId: newContactUserId,
                    Category: NotificationCategories.Visit,
                    IsActionRequired: false,
                    VisitRequestId: visitRequestId,
                    VisitInstanceId: visitInstanceId,
                    ActionType: NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={visitRequestId}"));

            if (previousContactUserId is not null && previousContactUserId != newContactUserId)
                Add(previousContactUserId.Value,
                    "Vai trò đầu mối vận hành đã được chuyển giao",
                    $"Vai trò đầu mối vận hành tại {campusLabel} của đơn {requestCode} đã chuyển cho người khác. Các cơ sở khác và tài khoản của bạn không bị ảnh hưởng.");

            if (registrantUserId is not null
                && registrantUserId != newContactUserId
                && registrantUserId != previousContactUserId)
                Add(registrantUserId.Value,
                    "Chuyển giao đầu mối vận hành đã hoàn tất",
                    $"Đầu mối vận hành mới tại {campusLabel} đã xác nhận cho đơn {requestCode}.");

            if (items.Count > 0)
                await notifications.CreateManyAsync(items, cancellationToken);
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex,
                "transfer-applied notification failed for visit instance {VisitInstanceId}", visitInstanceId);
        }
    }
}

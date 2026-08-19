using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Common;
using PEMS.Shared;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// Post-commit announcements for reception-host proposals (plan §7.3).
///
/// <para>
/// The distinction this class exists to hold is the whole point of the proposed-host model: being
/// PROPOSED and being ASSIGNED are different facts and must never read the same. Somebody told "you
/// are the host, go prepare" while the guest side has not confirmed will start preparing for a visit
/// that may never be confirmed — and if it falls through, nobody told them it was ever conditional.
/// </para>
///
/// <para>
/// Every dispatch here is best-effort and runs after commit: the campus state is already durable, so
/// a notification failure must not roll anything back. A failure to reach a Staff Leader who needs to
/// re-pick a host is logged at warning level, because that one leaves real work unclaimed.
/// </para>
/// </summary>
public static class ProposedHostNotifier
{
    public static async Task AnnounceOutcomeAsync(
        IApplicationDbContext db,
        INotificationService notifications,
        ILogger logger,
        VisitRequest visit,
        bool hadProposals,
        IReadOnlyList<ProposedHostActivation> activations,
        ulong actorUserId,
        CancellationToken ct)
    {
        if (!hadProposals && activations.Count == 0)
            return;

        try
        {
            var messages = new List<CreateNotificationRequest>();

            // Campuses whose proposal was stored but not activated yet: the gate is still shut.
            var activatedIds = activations.Select(a => a.VisitInstanceId).ToHashSet();
            var pending = visit.CampusInstances
                .Where(c => c.ProposedHostActivationStatus == ProposedHostActivationStatuses.Pending
                            && c.ProposedHostUserId is not null
                            && !activatedIds.Contains(c.VisitInstanceId))
                .ToList();

            var names = await CampusNamesAsync(db, visit, ct);
            var delegations = await DelegationNamesAsync(db, visit, ct);

            foreach (var instance in pending)
            {
                // Nobody needs telling that they proposed themself — they just did it.
                if (instance.ProposedHostUserId == instance.ProposedHostByUserId)
                    continue;

                messages.Add(new CreateNotificationRequest(
                    RecipientUserId: instance.ProposedHostUserId!.Value,
                    Title: "Bạn được đề xuất làm người phụ trách tiếp đón",
                    Message: $"Bạn được đề xuất làm người phụ trách tiếp đón đoàn {DelegationName(delegations, instance)} tại "
                             + $"{CampusName(names, instance)}, đang chờ đầu mối đoàn khách xác nhận. "
                             + "Chưa cần chuẩn bị cho tới khi được phân công chính thức.",
                    NotificationType: NotificationTypes.VisitStatusChanged,
                    RelatedType: NotificationRelatedTypes.VisitInstance,
                    RelatedId: instance.VisitInstanceId,
                    ActorUserId: actorUserId,
                    Category: NotificationCategories.Visit,
                    IsActionRequired: false,
                    VisitRequestId: visit.VisitRequestId,
                    VisitInstanceId: instance.VisitInstanceId,
                    CampusId: instance.CampusId,
                    ActionType: NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}",
                    MetadataJson: NotificationEventKeys.BuildMetadata(
                        NotificationEventKeys.HostProposalPending,
                        new { delegationName = DelegationName(delegations, instance), campusName = CampusName(names, instance) })));
            }

            foreach (var activation in activations)
            {
                var instance = visit.CampusInstances
                    .FirstOrDefault(c => c.VisitInstanceId == activation.VisitInstanceId);
                if (instance is null) continue;

                if (activation.Activated)
                {
                    // Now it IS an assignment, so it may finally say so.
                    if (activation.ProposedHostUserId is { } hostId
                        && hostId != activation.ProposerUserId)
                    {
                        messages.Add(new CreateNotificationRequest(
                            RecipientUserId: hostId,
                            Title: "Bạn được phân công phụ trách tiếp đón",
                            Message: $"Đầu mối đoàn khách đã xác nhận. Bạn là người phụ trách tiếp đón đoàn "
                                     + $"{DelegationName(delegations, instance)} tại {CampusName(names, instance)}. "
                                     + "Vui lòng bấm Bắt đầu chuẩn bị để mở các thao tác setup.",
                            NotificationType: NotificationTypes.HostAssigned,
                            RelatedType: NotificationRelatedTypes.VisitInstance,
                            RelatedId: instance.VisitInstanceId,
                            ActorUserId: activation.ProposerUserId,
                            Category: NotificationCategories.Visit,
                            IsActionRequired: true,
                            VisitRequestId: visit.VisitRequestId,
                            VisitInstanceId: instance.VisitInstanceId,
                            CampusId: instance.CampusId,
                            ActionType: NotificationActionTypes.OpenVisitDetail,
                            ActionUrl: $"/dashboard/visit/process/{instance.VisitInstanceId}",
                            MetadataJson: NotificationEventKeys.BuildMetadata(
                                NotificationEventKeys.HostAssigned,
                                new { delegationName = DelegationName(delegations, instance), campusName = CampusName(names, instance) })));
                    }
                    continue;
                }

                // Failed revalidation: the campus Staff Leader has to pick somebody. The guest-side
                // contact is deliberately NOT told — an FPTU staffing problem is not their business,
                // and their confirmation succeeded.
                var coordinatorId = instance.CoordinatorUserId;
                if (coordinatorId is null) continue;

                messages.Add(new CreateNotificationRequest(
                    RecipientUserId: coordinatorId.Value,
                    Title: "Host dự kiến cần được chọn lại",
                    Message: $"Đoàn {DelegationName(delegations, instance)} tại {CampusName(names, instance)} đã đủ điều kiện xử lý, "
                             + $"nhưng host dự kiến không còn hợp lệ ({activation.RejectionReason}). "
                             + "Vui lòng duyệt và chọn người phụ trách tiếp đón.",
                    NotificationType: NotificationTypes.VisitStatusChanged,
                    RelatedType: NotificationRelatedTypes.VisitInstance,
                    RelatedId: instance.VisitInstanceId,
                    ActorUserId: null,
                    Category: NotificationCategories.Visit,
                    IsActionRequired: true,
                    VisitRequestId: visit.VisitRequestId,
                    VisitInstanceId: instance.VisitInstanceId,
                    CampusId: instance.CampusId,
                    // OpenCampusReview: same action as VISIT_REQUEST_WAITING_APPROVAL — "Vui lòng
                    // duyệt và chọn người phụ trách tiếp đón" is the approve+assign-host control, not
                    // a passive detail view (plan-continuation §3 evidence).
                    ActionType: NotificationActionTypes.OpenCampusReview,
                    ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}",
                    MetadataJson: NotificationEventKeys.BuildMetadata(
                        NotificationEventKeys.HostReassignmentRequired,
                        new { campusName = CampusName(names, instance), reason = activation.RejectionReason })));
            }

            if (messages.Count > 0)
                await notifications.CreateManyAsync(messages, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Proposed-host notifications failed for visit request {VisitRequestId}; campus state is committed.",
                visit.VisitRequestId);
        }
    }

    private static async Task<Dictionary<ulong, string>> CampusNamesAsync(
        IApplicationDbContext db, VisitRequest visit, CancellationToken ct)
    {
        var ids = visit.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
        return await db.Campuses.AsNoTracking()
            .Where(c => ids.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, ct);
    }

    /// <summary>
    /// Delegation names come from each instance's OWN form detail — there is no request-level name,
    /// and borrowing a sibling's would put the wrong delegation in somebody's inbox.
    /// </summary>
    private static async Task<Dictionary<ulong, string>> DelegationNamesAsync(
        IApplicationDbContext db, VisitRequest visit, CancellationToken ct)
    {
        var ids = visit.CampusInstances.Select(c => c.VisitInstanceId).ToList();
        return await db.VisitInstanceFormDetails.AsNoTracking()
            .Where(d => ids.Contains(d.VisitInstanceId))
            .ToDictionaryAsync(d => d.VisitInstanceId, d => d.DelegationName, ct);
    }

    private static string CampusName(Dictionary<ulong, string> byCampus, VisitRequestCampus instance)
        => byCampus.TryGetValue(instance.CampusId, out var v) ? v : $"#{instance.CampusId}";

    private static string DelegationName(Dictionary<ulong, string> byInstance, VisitRequestCampus instance)
        => byInstance.TryGetValue(instance.VisitInstanceId, out var v) ? v : "đã đăng ký";
}

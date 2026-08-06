using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// Shared post-commit notification dispatch for both v2 create paths (authenticated + public OTP).
/// Routes an "incoming visit request" in-app notification to each involved campus Staff Leader (the
/// per-instance coordinator) plus, for a multi-campus request, a visibility notification to HO — mirroring
/// the v1 create. Best-effort: dispatched AFTER the request is committed, so a rollback never notifies and a
/// dispatch failure is logged but never rolls the committed request back (there is no outbox → not exactly-once).
/// The INITIAL_CLAIM invitation email to a different primary contact (contact ≠ registrant) is issued by the
/// identity-confirmation workflow (Phase D), not here.
/// </summary>
internal static class V2CreateNotifier
{
    public static async Task NotifyStaffLeadersAfterCommitAsync(
        IApplicationDbContext db,
        INotificationService notificationService,
        ILogger logger,
        VisitRequest created,
        CancellationToken cancellationToken)
    {
        try
        {
            // ONLY campuses still awaiting a decision get the actionable "please review" notification.
            // A campus the authenticated creator already processed directly (self-host / leader-assign) is
            // not pending for anyone, so raising an action there would be a fake task.
            var pendingInstances = created.CampusInstances
                .Where(c => c.Status == VisitInstanceStatus.WaitingRequestApproval && c.CoordinatorUserId.HasValue)
                .ToList();

            // Per-campus names: each Staff Leader is told about THEIR OWN campus's delegation, so a mixed
            // request never shows one campus's content to another campus's reviewer.
            var pendingInstanceIds = pendingInstances.Select(c => c.VisitInstanceId).ToList();
            var nameByInstance = pendingInstanceIds.Count == 0
                ? new Dictionary<ulong, string>()
                : await db.VisitInstanceFormDetails.AsNoTracking()
                    .Where(d => pendingInstanceIds.Contains(d.VisitInstanceId))
                    .ToDictionaryAsync(d => d.VisitInstanceId, d => d.DelegationName, cancellationToken);

            var notifications = pendingInstances.Select(c => new CreateNotificationRequest(
                RecipientUserId: c.CoordinatorUserId!.Value,
                Title: "Có yêu cầu tiếp khách mới",
                Message: $"{(nameByInstance.TryGetValue(c.VisitInstanceId, out var nm) ? nm : created.RequestCode)} đang chờ xử lý tại cơ sở của bạn. Vui lòng xem chi tiết, duyệt/từ chối và chọn host nếu duyệt.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                RelatedId: created.VisitRequestId,
                Category: NotificationCategories.Visit,
                IsActionRequired: true,
                VisitRequestId: created.VisitRequestId,
                ActionType: NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit?visitRequestId={created.VisitRequestId}")).ToList();

            if (created.VisitScope == VisitScopes.MultiCampus)
            {
                var hoUsers = await db.Users
                    .Where(u => u.Role.RoleCode == RoleCodes.Ho && u.Status == UserStatuses.Active)
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);
                notifications.AddRange(hoUsers.Select(id => new CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Có đơn liên cơ sở mới",
                    // Request-level message: a mixed request has no single business name.
                    Message: $"{(created.HasMixedCampusDetails ? "Khác nhau theo cơ sở" : nameByInstance.Values.FirstOrDefault() ?? created.RequestCode)} vừa gửi đơn liên cơ sở, đang chờ các cơ sở xử lý.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: created.VisitRequestId,
                    Category: NotificationCategories.Visit,
                    IsActionRequired: false,
                    VisitRequestId: created.VisitRequestId,
                    ActionType: NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={created.VisitRequestId}")));
            }

            if (notifications.Count > 0)
                await notificationService.CreateManyAsync(notifications, cancellationToken);
        }
        catch (System.Exception ex)
        {
            // The request is already committed; a notification failure must not fail the create.
            logger.LogError(ex,
                "create-v2 post-commit notification dispatch failed for visit request {VisitRequestId}",
                created.VisitRequestId);
        }
    }

    /// <summary>
    /// Post-commit INITIAL_CLAIM invitation (plan §16.4): when the primary contact is NOT the registrant,
    /// the create transaction stored a PENDING claim — this sends the invitation email with the single-use
    /// campus that needs one. A campus whose contact matched the registrant was linked inside the create
    /// transaction and has no invitation — nothing is sent for it, by design. Only the first successful
    /// create reaches here (idempotent replays return earlier), so a retry never re-invites. Best-effort
    /// PER CAMPUS: one address failing must not deny the others theirs, and any that fails stays PENDING
    /// for the registrant to resend.
    /// </summary>
    public static async Task SendOperationalContactInvitationsAfterCommitAsync(
        IApplicationDbContext db,
        IOperationalContactInvitationService invitations,
        ILogger logger,
        VisitRequest created,
        CancellationToken cancellationToken)
    {
        List<ulong> pending;
        try
        {
            pending = await db.VisitRequestIdentityChanges.AsNoTracking()
                .Where(c => c.VisitRequestId == created.VisitRequestId
                            && c.ChangeKind == IdentityChangeKinds.InitialConfirmation
                            && c.Status == IdentityChangeStatuses.Pending)
                .OrderBy(c => c.VisitInstanceId)
                .Select(c => c.IdentityChangeId)
                .ToListAsync(cancellationToken);
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex,
                "create-v2 could not read pending operational-contact invitations for visit request {VisitRequestId}",
                created.VisitRequestId);
            return;
        }

        foreach (var id in pending)
        {
            try
            {
                await invitations.SendInvitationAsync(id, cancellationToken);
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex,
                    "create-v2 post-commit operational-contact invitation failed for identity change {IdentityChangeId} of visit request {VisitRequestId}",
                    id, created.VisitRequestId);
            }
        }
    }
}

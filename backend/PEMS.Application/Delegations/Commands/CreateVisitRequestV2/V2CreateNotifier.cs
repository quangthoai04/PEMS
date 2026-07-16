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
            var leaderIds = created.CampusInstances
                .Where(c => c.CoordinatorUserId.HasValue)
                .Select(c => c.CoordinatorUserId!.Value)
                .Distinct()
                .ToList();

            var notifications = leaderIds.Select(id => new CreateNotificationRequest(
                RecipientUserId: id,
                Title: "Có yêu cầu tiếp khách mới",
                Message: $"{created.DelegationName} đang chờ xử lý tại cơ sở của bạn. Vui lòng xem chi tiết, duyệt/từ chối và chọn host nếu duyệt.",
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
                    Message: $"{created.DelegationName} vừa gửi đơn liên cơ sở, đang chờ các cơ sở xử lý.",
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
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Notifications;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
namespace PEMS.Application.DepartmentReceptionTasks.Commands.DeclineAssignedLogisticsTask
{
    public class DeclineAssignedLogisticsTaskCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
        public string Reason { get; set; }
    }

    public class DeclineAssignedLogisticsTaskCommandHandler : IRequestHandler<DeclineAssignedLogisticsTaskCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
        private readonly IUserMutationLockService _lockService;

        public DeclineAssignedLogisticsTaskCommandHandler(
            IApplicationDbContext context, ICurrentUserService currentUserService,
            PEMS.Application.Notifications.Common.INotificationService notificationService,
            IUserMutationLockService lockService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _notificationService = notificationService;
            _lockService = lockService;
        }

        public async Task<bool> Handle(DeclineAssignedLogisticsTaskCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new Exception("Vui lòng nhập lý do từ chối");
            if (request.Reason.Trim().Length > 1000)
                throw new Exception("Lý do từ chối không được quá 1000 ký tự");

            ulong userId = _currentUserService.UserId.Value;

            var visitInstanceId = await _context.VisitLogisticsItems
                .Where(x => x.LogisticsItemId == request.LogisticsItemId)
                .Select(x => (ulong?)x.VisitInstanceId)
                .FirstOrDefaultAsync(cancellationToken);
            if (visitInstanceId is null) throw new Exception("Không tìm thấy nhiệm vụ");
            var visitRequestId = await _context.VisitRequestCampuses
                .Where(c => c.VisitInstanceId == visitInstanceId)
                .Select(c => (ulong?)c.VisitRequestId)
                .FirstOrDefaultAsync(cancellationToken) ?? throw new Exception("Không tìm thấy chuyến tiếp khách");

            // Lock hierarchy — same reasoning as AcceptAssignedLogisticsTaskCommand.
            await using var transaction = await _context.BeginSerializedTransactionAsync(cancellationToken);
            await _lockService.LockVisitRequestsAsync(new[] { visitRequestId }, cancellationToken);
            await _lockService.LockVisitRequestCampusesAsync(new[] { visitInstanceId.Value }, cancellationToken);
            await _lockService.LockVisitLogisticsItemsAsync(new[] { request.LogisticsItemId }, cancellationToken);

            var l = await _context.VisitLogisticsItems
                .Include(x => x.VisitInstance)
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new Exception("Không tìm thấy nhiệm vụ");
            if (l.AssignedToUserId != userId) throw new Exception("Bạn không phải người được phân công nhiệm vụ này");
            if (l.Status != "ASSIGNED") throw new Exception("Nhiệm vụ không ở trạng thái chờ xác nhận");

            // Get latest PENDING attempt for this user
            var attempt = await _context.VisitLogisticsAssignmentAttempts
                .Where(a => a.LogisticsItemId == request.LogisticsItemId
                         && a.AssigneeUserId == userId
                         && a.Status == "PENDING")
                .OrderByDescending(a => a.AssignedAt)
                .FirstOrDefaultAsync(cancellationToken);
            // Legacy/seeded rows may not have a PENDING attempt, but AssignedToUserId
            // still proves this staff member is the current assignee.

            var decliningUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            string decliningName = decliningUser?.FullName ?? "Nhân viên";

            if (attempt != null)
            {
                attempt.Status = "DECLINED";
                attempt.RespondedAt = VietnamTime.Now();
                attempt.ResponseNote = request.Reason.Trim();
                attempt.ResponseSource = "PORTAL";
                attempt.UpdatedAt = VietnamTime.Now();
            }

            // Staff declining an assignment is terminal (spec §1.2/§6): DECLINED, not REJECTED —
            // REJECTED is reserved for a Leader rejecting the Host's original request. No reassignment
            // follows (AssignRequestAssigneeCommand already blocks it via blockedStatuses), and the
            // assignment history (who assigned it, to whom, when) is kept rather than nulled out, so the
            // record can still answer "ai giao, giao cho ai, người đó đã từ chối".
            l.Status = PEMS.Shared.LogisticsItemStatus.Declined;
            l.AssigneeResponseNote = request.Reason.Trim();
            l.UpdatedBy = userId;
            l.UpdatedAt = VietnamTime.Now();

            // A Portal response retires any pending emailed accept/decline link for this item (BUG-05).
            await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _context, PEMS.Domain.Constants.EmailActionTargetTypes.LogisticsItem, l.LogisticsItemId,
                "Nhiệm vụ đã được xử lý trong hệ thống.", VietnamTime.Now(), cancellationToken);

            var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
            ulong? assignedBy = attempt?.AssignedBy ?? l.AssignedBy;
            var hostId = l.VisitInstance?.CurrentHostUserId;
            var deptTaskUrl = l.RequestedToDepartmentId.HasValue
                ? $"/dashboard/departments/{l.RequestedToDepartmentId.Value}/tasks/{l.LogisticsItemId}"
                : null;
            var delegationName = (await PEMS.Application.Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
                .ForInstancesAsync(_context, new[] { l.VisitInstanceId }, cancellationToken))
                .GetValueOrDefault(l.VisitInstanceId) ?? l.Title;
            var declinedMetadataJson = PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                PEMS.Application.Notifications.Common.NotificationEventKeys.LogisticsAssigneeDeclined,
                new { delegationName, reason = l.AssigneeResponseNote });

            if (assignedBy.HasValue && assignedBy.Value != userId)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: assignedBy.Value,
                    Title: "Nhân viên từ chối nhiệm vụ",
                    Message: $"Nhân viên {decliningName} đã từ chối nhiệm vụ \"{l.Title}\".",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsAssigneeResponded,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    RelatedId: request.LogisticsItemId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                    IsActionRequired: false,
                    VisitRequestId: l.VisitInstance?.VisitRequestId,
                    VisitInstanceId: l.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                    ActionUrl: deptTaskUrl ?? $"/dashboard/visit/process/{l.VisitInstanceId}",
                    MetadataJson: declinedMetadataJson
                ));
            }
            else if (l.RequestedToDepartmentId.HasValue)
            {
                var dept = await _context.Departments.FirstOrDefaultAsync(d => d.DepartmentId == l.RequestedToDepartmentId.Value, cancellationToken);
                if (dept?.HeadUserId != null && dept.HeadUserId.Value != userId)
                {
                    notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: dept.HeadUserId.Value,
                        Title: "Nhân viên từ chối nhiệm vụ",
                        Message: $"Nhân viên {decliningName} đã từ chối nhiệm vụ \"{l.Title}\".",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsAssigneeResponded,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                        RelatedId: request.LogisticsItemId,
                        ActorUserId: userId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                        IsActionRequired: false,
                        VisitRequestId: l.VisitInstance?.VisitRequestId,
                        VisitInstanceId: l.VisitInstanceId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                        ActionUrl: deptTaskUrl!,
                        MetadataJson: declinedMetadataJson
                    ));
                }
            }

            if (hostId.HasValue && hostId.Value != userId && hostId.Value != assignedBy)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: hostId.Value,
                    Title: "Nhân viên từ chối nhiệm vụ",
                    Message: $"Nhân sự phòng ban đã từ chối nhiệm vụ hậu cần: {l.Title}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsAssigneeResponded,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    RelatedId: request.LogisticsItemId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                    IsActionRequired: false,
                    VisitInstanceId: l.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit/process/{l.VisitInstanceId}",
                    MetadataJson: declinedMetadataJson
                ));
            }

            if (notifications.Any())
            {
                await _notificationService.CreateManyAsync(notifications, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }
}

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

        public DeclineAssignedLogisticsTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, PEMS.Application.Notifications.Common.INotificationService notificationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _notificationService = notificationService;
        }

        public async Task<bool> Handle(DeclineAssignedLogisticsTaskCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new Exception("Vui lòng nhập lý do từ chối");
            if (request.Reason.Trim().Length > 1000)
                throw new Exception("Lý do từ chối không được quá 1000 ký tự");

            ulong userId = _currentUserService.UserId.Value;

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

            // Staff declines the assignment attempt; leader can reassign while history is kept.
            l.Status = "REJECTED";
            l.AssignedToUserId = null;
            l.AssignedBy = null;
            l.AssignedAt = null;
            l.AssigneeResponseNote = request.Reason.Trim();
            l.UpdatedBy = userId;
            l.UpdatedAt = VietnamTime.Now();

            var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
            ulong? assignedBy = attempt?.AssignedBy ?? l.AssignedBy;
            var hostId = l.VisitInstance?.CurrentHostUserId;
            var deptTaskUrl = l.RequestedToDepartmentId.HasValue
                ? $"/dashboard/departments/{l.RequestedToDepartmentId.Value}/tasks/{l.LogisticsItemId}"
                : null;

            if (assignedBy.HasValue && assignedBy.Value != userId)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: assignedBy.Value,
                    Title: "Nhân viên từ chối nhiệm vụ",
                    Message: $"Nhân viên {decliningName} đã từ chối nhiệm vụ \"{l.Title}\". Vui lòng phân công người khác.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsAssigneeResponded,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    RelatedId: request.LogisticsItemId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                    IsActionRequired: true,
                    VisitRequestId: l.VisitInstance?.VisitRequestId,
                    VisitInstanceId: l.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                    ActionUrl: deptTaskUrl ?? $"/dashboard/visit/process/{l.VisitInstanceId}"
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
                        Message: $"Nhân viên {decliningName} đã từ chối nhiệm vụ \"{l.Title}\". Vui lòng phân công người khác.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsAssigneeResponded,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                        RelatedId: request.LogisticsItemId,
                        ActorUserId: userId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                        IsActionRequired: true,
                        VisitRequestId: l.VisitInstance?.VisitRequestId,
                        VisitInstanceId: l.VisitInstanceId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                        ActionUrl: deptTaskUrl!
                    ));
                }
            }

            if (hostId.HasValue && hostId.Value != userId && hostId.Value != assignedBy)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: hostId.Value,
                    Title: "Nhân viên từ chối nhiệm vụ",
                    Message: $"Nhân sự phòng ban đã từ chối nhiệm vụ hậu cần: {l.Title}. Phòng ban sẽ phân công người khác.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsAssigneeResponded,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    RelatedId: request.LogisticsItemId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                    IsActionRequired: false,
                    VisitInstanceId: l.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit/process/{l.VisitInstanceId}"
                ));
            }

            if (notifications.Any())
            {
                await _notificationService.CreateManyAsync(notifications, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}

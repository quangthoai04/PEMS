using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
namespace PEMS.Application.DepartmentReceptionTasks.Commands.AcceptAssignedLogisticsTask
{
    public class AcceptAssignedLogisticsTaskCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
    }

    public class AcceptAssignedLogisticsTaskCommandHandler : IRequestHandler<AcceptAssignedLogisticsTaskCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

        public AcceptAssignedLogisticsTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, PEMS.Application.Notifications.Common.INotificationService notificationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _notificationService = notificationService;
        }

        public async Task<bool> Handle(AcceptAssignedLogisticsTaskCommand request, CancellationToken cancellationToken)
        {
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
                         && a.AssigneeUserId == userId)
                .OrderByDescending(a => a.AssignedAt)
                .FirstOrDefaultAsync(cancellationToken);

            // Legacy rows may not have an assignment-attempt record; the item assignment above is authoritative.

            // Update attempt
            if (attempt != null)
            {
                attempt.Status = "ACCEPTED";
                attempt.RespondedAt = VietnamTime.Now();
                attempt.ResponseSource = "PORTAL";
                attempt.UpdatedAt = VietnamTime.Now();
            }

            // Update item
            l.Status = "ACCEPTED";
            l.AssigneeAcceptedAt = VietnamTime.Now();
            l.AssigneeResponseNote = null;
            l.UpdatedBy = userId;
            l.UpdatedAt = VietnamTime.Now();

            await _context.SaveChangesAsync(cancellationToken);

            var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
            var assignedBy = l.AssignedBy; // Department Leader who assigned this
            var hostId = l.VisitInstance?.CurrentHostUserId;
            var deptTaskUrl = l.RequestedToDepartmentId.HasValue
                ? $"/dashboard/departments/{l.RequestedToDepartmentId.Value}/tasks/{l.LogisticsItemId}"
                : null;
            var delegationName = (await PEMS.Application.Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
                .ForInstancesAsync(_context, new[] { l.VisitInstanceId }, cancellationToken))
                .GetValueOrDefault(l.VisitInstanceId) ?? l.Title;
            var acceptedMetadataJson = PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                PEMS.Application.Notifications.Common.NotificationEventKeys.LogisticsAssigneeAccepted,
                new { delegationName });

            if (assignedBy.HasValue && assignedBy.Value != userId)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: assignedBy.Value,
                    Title: "Logistics được tiếp nhận",
                    Message: $"Nhân sự đã tiếp nhận nhiệm vụ hậu cần: {l.Title}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsAssigneeResponded,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    RelatedId: request.LogisticsItemId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                    VisitRequestId: l.VisitInstance?.VisitRequestId,
                    VisitInstanceId: l.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenLogisticsDetail,
                    ActionUrl: deptTaskUrl ?? $"/dashboard/visit/process/{l.VisitInstanceId}",
                    MetadataJson: acceptedMetadataJson
                ));
            }

            if (hostId.HasValue && hostId.Value != userId && hostId.Value != assignedBy)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: hostId.Value,
                    Title: "Logistics được tiếp nhận",
                    Message: $"Nhân sự phòng ban đã tiếp nhận nhiệm vụ hậu cần: {l.Title}.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.LogisticsAssigneeResponded,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.LogisticsItem,
                    RelatedId: request.LogisticsItemId,
                    ActorUserId: userId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Logistics,
                    VisitInstanceId: l.VisitInstanceId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit/process/{l.VisitInstanceId}",
                    MetadataJson: acceptedMetadataJson
                ));
            }

            if (notifications.Any())
            {
                await _notificationService.CreateManyAsync(notifications, cancellationToken);
            }

            return true;
        }
    }
}

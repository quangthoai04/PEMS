using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                attempt.RespondedAt = DateTime.UtcNow;
                attempt.ResponseSource = "PORTAL";
                attempt.UpdatedAt = DateTime.UtcNow;
            }

            // Update item
            l.Status = "ACCEPTED";
            l.AssigneeAcceptedAt = DateTime.UtcNow;
            l.AssigneeResponseNote = null;
            l.UpdatedBy = userId;
            l.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            if (l.VisitInstance?.CurrentHostUserId != null)
            {
                await _notificationService.CreateAsync(
                    recipientUserId: l.VisitInstance.CurrentHostUserId.Value,
                    title: "Logistics được tiếp nhận",
                    message: "Nhiệm vụ logistics của bạn đã được tiếp nhận.",
                    notificationType: PEMS.Domain.Constants.NotificationTypes.LogisticsAssigneeResponded,
                    relatedType: PEMS.Domain.Constants.NotificationRelatedTypes.LogisticsItem,
                    relatedId: request.LogisticsItemId,
                    cancellationToken: cancellationToken);
            }

            return true;
        }
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Notifications;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public DeclineAssignedLogisticsTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(DeclineAssignedLogisticsTaskCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new Exception("Vui lòng nhập lý do từ chối");
            if (request.Reason.Trim().Length > 1000)
                throw new Exception("Lý do từ chối không được quá 1000 ký tự");

            ulong userId = _currentUserService.UserId.Value;

            var l = await _context.VisitLogisticsItems
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

            if (attempt == null) throw new Exception("Không tìm thấy lần phân công hợp lệ");

            var decliningUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            string decliningName = decliningUser?.FullName ?? "Nhân viên";

            // Update attempt
            attempt.Status = "DECLINED";
            attempt.RespondedAt = DateTime.UtcNow;
            attempt.ResponseNote = request.Reason.Trim();
            attempt.ResponseSource = "PORTAL";
            attempt.UpdatedAt = DateTime.UtcNow;

            // Staff declines the assignment attempt; leader can reassign while history is kept.
            l.Status = "DECLINED";
            l.AssignedToUserId = null;
            l.AssignedBy = null;
            l.AssignedAt = null;
            l.AssigneeResponseNote = request.Reason.Trim();
            l.UpdatedBy = userId;
            l.UpdatedAt = DateTime.UtcNow;

            // Notify Department Leader if we can identify them
            if (l.RequestedToDepartmentId.HasValue)
            {
                var dept = await _context.Departments
                    .FirstOrDefaultAsync(d => d.DepartmentId == l.RequestedToDepartmentId.Value, cancellationToken);

                ulong? leaderId = dept?.HeadUserId;
                if (leaderId.HasValue)
                {
                    _context.Notifications.Add(new Notification
                    {
                        RecipientUserId = leaderId.Value,
                        Title = "Nhân viên từ chối nhiệm vụ",
                        Message = $"Nhân viên {decliningName} đã từ chối nhiệm vụ \"{l.Title}\". Vui lòng phân công người khác.",
                        NotificationType = "LOGISTICS_DECLINED",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}

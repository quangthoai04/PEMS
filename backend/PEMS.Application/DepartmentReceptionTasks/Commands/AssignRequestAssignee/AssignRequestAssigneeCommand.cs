using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Entities.Delegations;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.AssignRequestAssignee
{
    public class AssignRequestAssigneeCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
        public ulong AssigneeUserId { get; set; }
    }

    public class AssignRequestAssigneeCommandHandler : IRequestHandler<AssignRequestAssigneeCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public AssignRequestAssigneeCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(AssignRequestAssigneeCommand request, CancellationToken cancellationToken)
        {
            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null) throw new Exception("Không xác định được người dùng hiện tại");

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new Exception("Không tìm thấy đơn yêu cầu");

            // Check department scope
            if (l.RequestedToDepartmentId != user.DepartmentId)
                throw new Exception("Không có quyền phân công đơn yêu cầu của phòng ban khác");

            // Block assignment in terminal statuses
            var blockedStatuses = new[] { "ACCEPTED", "IN_PROGRESS", "READY", "DONE", "CANCELLED", "REJECTED" };
            if (blockedStatuses.Contains(l.Status))
                throw new Exception("Không thể phân công khi nhiệm vụ đang ở trạng thái: " + l.Status);

            // Check if already has an active PENDING attempt
            bool hasPendingAttempt = await _context.VisitLogisticsAssignmentAttempts
                .AnyAsync(a => a.LogisticsItemId == request.LogisticsItemId && a.Status == "PENDING", cancellationToken);
            if (hasPendingAttempt)
                throw new ConflictException("Nhiệm vụ đã được phân công và đang chờ phản hồi hoặc đã được nhận.");

            // Check handover (cannot reassign if signed)
            bool hasSigned = await _context.VisitLogisticsItemHandovers
                .AnyAsync(h => h.LogisticsItemId == request.LogisticsItemId &&
                               (h.BorrowerSignedAt != null || h.ProviderSignedAt != null), cancellationToken);
            if (hasSigned)
                throw new Exception("Nhiệm vụ đã được xử lý hoặc đã có ký biên bản, không thể đổi người phụ trách.");

            // Validate assignee: same department, ACTIVE
            var assignee = await _context.Users.FirstOrDefaultAsync(
                u => u.UserId == request.AssigneeUserId
                     && u.DepartmentId == user.DepartmentId
                     && u.Status == "ACTIVE",
                cancellationToken);
            if (assignee == null)
                throw new Exception("Người phụ trách không hợp lệ hoặc không thuộc phòng ban");

            // Insert assignment attempt
            var attempt = new VisitLogisticsAssignmentAttempt
            {
                LogisticsItemId = request.LogisticsItemId,
                AssigneeUserId = request.AssigneeUserId,
                AssignedBy = userId,
                AssignedAt = DateTime.UtcNow,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };
            _context.VisitLogisticsAssignmentAttempts.Add(attempt);

            // Update item
            l.AssignedToUserId = request.AssigneeUserId;
            l.AssignedBy = userId;
            l.AssignedAt = DateTime.UtcNow;
            l.Status = "ASSIGNED";
            l.UpdatedBy = userId;
            l.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}

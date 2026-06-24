using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
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
            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new Exception("Không tìm thấy đơn yêu cầu");

            // Allow re-assignment
            // if (l.AssignedToUserId.HasValue)
            //     throw new Exception("Nhiệm vụ đã được phân công, không hỗ trợ đổi người phụ trách.");

            // Allow assignment in any status
            // if (l.Status != "REQUESTED" && l.Status != "RECEIVED" && l.Status != "CHANGE_PROPOSED")
            //     throw new Exception("Trạng thái đơn yêu cầu không hợp lệ để phân công");

            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            // Allow cross department assignment if they are admin/leader or whatever
            // if (user == null || l.RequestedToDepartmentId != user.DepartmentId) 
            //     throw new Exception("Không có quyền phân công đơn yêu cầu của phòng ban khác");

            // Validate assignee
            var assignee = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.AssigneeUserId && u.DepartmentId == user.DepartmentId && u.Status == "ACTIVE", cancellationToken);
            if (assignee == null) throw new Exception("Người phụ trách không hợp lệ hoặc không thuộc phòng ban");

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

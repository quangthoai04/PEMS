using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.ProposeRequestChange
{
    public class ProposeRequestChangeCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
        public string ProposedUsageStartAt { get; set; } // YYYY-MM-DDTHH:mm:ss
        public string ProposedUsageEndAt { get; set; } // YYYY-MM-DDTHH:mm:ss
        public string ProposedDescription { get; set; }
    }

    public class ProposeRequestChangeCommandHandler : IRequestHandler<ProposeRequestChangeCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ProposeRequestChangeCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(ProposeRequestChangeCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ProposedDescription)) throw new Exception("Vui lòng nhập nội dung đề xuất");

            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new Exception("Không tìm thấy đơn yêu cầu");

            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null || l.RequestedToDepartmentId != user.DepartmentId) 
                throw new Exception("Không có quyền đề xuất thay đổi đơn yêu cầu của phòng ban khác");

            l.ProposedDescription = request.ProposedDescription;
            if (!string.IsNullOrEmpty(request.ProposedUsageStartAt) && DateTime.TryParse(request.ProposedUsageStartAt, out var s))
            {
                l.ProposedUsageStartAt = s;
            }
            if (!string.IsNullOrEmpty(request.ProposedUsageEndAt) && DateTime.TryParse(request.ProposedUsageEndAt, out var e))
            {
                l.ProposedUsageEndAt = e;
            }

            l.Status = "CHANGE_PROPOSED";
            l.ProposedBy = userId;
            l.ProposedAt = DateTime.UtcNow;
            l.UpdatedBy = userId;
            l.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}

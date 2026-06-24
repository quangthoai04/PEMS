using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.ConfirmRequest
{
    public class ConfirmRequestCommand : IRequest<bool>
    {
        public ulong LogisticsItemId { get; set; }
    }

    public class ConfirmRequestCommandHandler : IRequestHandler<ConfirmRequestCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ConfirmRequestCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(ConfirmRequestCommand request, CancellationToken cancellationToken)
        {
            var l = await _context.VisitLogisticsItems
                .FirstOrDefaultAsync(x => x.LogisticsItemId == request.LogisticsItemId, cancellationToken);

            if (l == null) throw new Exception("Không tìm thấy đơn yêu cầu");

            // Verify department
            ulong userId = _currentUserService.UserId.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null || l.RequestedToDepartmentId != user.DepartmentId) 
                throw new Exception("Không có quyền xác nhận đơn yêu cầu của phòng ban khác");

            // if (l.Status == "REQUESTED")
            // {
                l.Status = "RECEIVED";
                l.ReceivedBy = userId;
                l.ReceivedAt = DateTime.UtcNow;
                l.UpdatedBy = userId;
                l.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);
            // }

            return true;
        }
    }
}

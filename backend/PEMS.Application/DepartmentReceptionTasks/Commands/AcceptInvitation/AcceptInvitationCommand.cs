using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.AcceptInvitation
{
    public class AcceptInvitationCommand : IRequest<bool>
    {
        public ulong ParticipantId { get; set; }
    }

    public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public AcceptInvitationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
        {
            var p = await _context.VisitParticipants
                .FirstOrDefaultAsync(x => x.ParticipantId == request.ParticipantId, cancellationToken);

            if (p == null) throw new Exception("Không tìm thấy thư mời");

            // Chỉ cho ACCEPT nếu status = INVITED
            // Allow accepting anytime
            // if (p.Status != "INVITED") throw new Exception("Thư mời không ở trạng thái chờ xác nhận.");

            p.Status = "ACCEPTED";
            p.RespondedAt = DateTime.UtcNow;
            p.UpdatedBy = _currentUserService.UserId;
            p.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}

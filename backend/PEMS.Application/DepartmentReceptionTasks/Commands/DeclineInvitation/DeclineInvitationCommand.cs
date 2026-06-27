using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentReceptionTasks.Commands.DeclineInvitation
{
    public class DeclineInvitationCommand : IRequest<bool>
    {
        public ulong ParticipantId { get; set; }
        public string Reason { get; set; }
    }

    public class DeclineInvitationCommandHandler : IRequestHandler<DeclineInvitationCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public DeclineInvitationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(DeclineInvitationCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new Exception("Vui lòng nhập lý do từ chối");

            var p = await _context.VisitParticipants
                .FirstOrDefaultAsync(x => x.ParticipantId == request.ParticipantId, cancellationToken);

            if (p == null) throw new Exception("Không tìm thấy thư mời");

            // Allow declining anytime
            // if (p.Status != "INVITED") throw new Exception("Thư mời không ở trạng thái chờ xác nhận.");

            p.Status = "DECLINED";
            p.Note = request.Reason;
            p.RespondedAt = DateTime.UtcNow;
            p.UpdatedBy = _currentUserService.UserId;
            p.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}

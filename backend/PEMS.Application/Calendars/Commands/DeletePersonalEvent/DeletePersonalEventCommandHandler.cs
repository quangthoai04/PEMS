using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Calendars.Commands.DeletePersonalEvent
{
    public sealed class DeletePersonalEventCommandHandler : IRequestHandler<DeletePersonalEventCommand, bool>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public DeletePersonalEventCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(DeletePersonalEventCommand request, CancellationToken cancellationToken)
        {
            var ev = await _context.CalendarEvents.FirstOrDefaultAsync(e => e.CalendarEventId == request.CalendarEventId, cancellationToken);
            if (ev == null) return true;

            ulong userId = _currentUserService.UserId.Value;
            if (ev.OwnerUserId != userId) throw new ForbiddenException("Không có quyền xóa lịch cá nhân này");

            _context.CalendarEvents.Remove(ev);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

using PEMS.Application.Common;
namespace PEMS.Application.Notifications.Commands.MarkNotificationAsRead;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkNotificationAsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            // SEC-19: was UnauthorizedAccessException, unmatched by ExceptionHandlingMiddleware and
            // surfaced as a generic 500. ForbiddenException matches the "not authenticated inside the
            // handler" idiom used throughout this codebase and maps to a clean 403.
            throw new ForbiddenException();
        }

        // SEC-19: a single combined query + a single NotFoundException (was a bare `Exception`, also
        // an unmatched 500) — deliberately NOT split into a separate "wrong owner" ForbiddenException,
        // so a notification id the caller may not touch stays indistinguishable from one that does not
        // exist at all (the same anti-enumeration principle VisitLinkSupport documents explicitly).
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == request.NotificationId && n.RecipientUserId == userId.Value, cancellationToken)
            ?? throw new NotFoundException("Notification", request.NotificationId);

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = VietnamTime.Now();
            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}

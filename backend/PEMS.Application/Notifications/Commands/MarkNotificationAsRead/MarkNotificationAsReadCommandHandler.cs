using MediatR;
using Microsoft.EntityFrameworkCore;
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
            throw new UnauthorizedAccessException();
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == request.NotificationId && n.RecipientUserId == userId.Value, cancellationToken);

        if (notification == null)
        {
            throw new Exception("Notification not found or access denied.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = VietnamTime.Now();
            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}

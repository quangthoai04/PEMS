using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

using PEMS.Application.Common;
namespace PEMS.Application.Notifications.Commands.MarkAllNotificationsAsRead;

public class MarkAllNotificationsAsReadCommandHandler : IRequestHandler<MarkAllNotificationsAsReadCommand, MarkAllNotificationsAsReadResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkAllNotificationsAsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MarkAllNotificationsAsReadResponse> Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException();
        }

        var unreadNotifications = await _context.Notifications
            .Where(n => n.RecipientUserId == userId.Value && !n.IsRead)
            .ToListAsync(cancellationToken);

        var count = 0;
        if (unreadNotifications.Any())
        {
            var now = VietnamTime.Now();
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
                count++;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new MarkAllNotificationsAsReadResponse(count);
    }
}

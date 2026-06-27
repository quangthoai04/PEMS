using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;

namespace PEMS.Application.PublicContent.Commands.MarkNotificationsRead;

public sealed class MarkNotificationsReadCommandHandler : IRequestHandler<MarkNotificationsReadCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService   _currentUser;

    public MarkNotificationsReadCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext   = dbContext;
        _currentUser = currentUser;
    }

    public async Task Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var now = DateTime.UtcNow;

        if (request.NotificationId.HasValue)
        {
            var notification = await _dbContext.Notifications
                .Where(n => n.NotificationId == request.NotificationId.Value
                         && n.RecipientUserId == currentUserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (notification is not null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
            }
        }
        else
        {
            var unread = await _dbContext.Notifications
                .Where(n => n.RecipientUserId == currentUserId && !n.IsRead)
                .ToListAsync(cancellationToken);

            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

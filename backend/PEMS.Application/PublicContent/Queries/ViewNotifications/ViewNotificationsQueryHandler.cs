using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;

namespace PEMS.Application.PublicContent.Queries.ViewNotifications;

public sealed class ViewNotificationsQueryHandler : IRequestHandler<ViewNotificationsQuery, ViewNotificationsDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService   _currentUser;

    public ViewNotificationsQueryHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext   = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ViewNotificationsDto> Handle(ViewNotificationsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var items = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == currentUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(pageSize)
            .Select(n => new NotificationItemDto
            {
                NotificationId   = n.NotificationId,
                NotificationType = n.NotificationType,
                Title            = n.Title,
                Message          = n.Message,
                RelatedType      = n.RelatedType,
                RelatedId        = n.RelatedId,
                IsRead           = n.IsRead,
                CreatedAt        = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var unreadCount = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == currentUserId && !n.IsRead, cancellationToken);

        return new ViewNotificationsDto
        {
            Items       = items,
            UnreadCount = unreadCount
        };
    }
}

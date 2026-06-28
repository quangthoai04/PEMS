using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Notifications.Queries.GetMyUnreadNotificationCount;

public class GetMyUnreadNotificationCountQueryHandler : IRequestHandler<GetMyUnreadNotificationCountQuery, UnreadNotificationCountResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyUnreadNotificationCountQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<UnreadNotificationCountResponse> Handle(GetMyUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException();
        }

        var unreadCount = await _context.Notifications
            .Where(n => n.RecipientUserId == userId.Value && !n.IsRead)
            .CountAsync(cancellationToken);

        return new UnreadNotificationCountResponse(unreadCount);
    }
}

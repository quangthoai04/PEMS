using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Notifications.Common;

namespace PEMS.Application.Notifications.Queries.GetMyNotifications;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, PaginatedResult<NotificationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationTargetResolver _targetResolver;

    public GetMyNotificationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService, INotificationTargetResolver targetResolver)
    {
        _context = context;
        _currentUserService = currentUserService;
        _targetResolver = targetResolver;
    }

    public async Task<PaginatedResult<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException();
        }

        var query = _context.Notifications
            .Where(n => n.RecipientUserId == userId.Value)
            .AsNoTracking();

        if (request.IsRead.HasValue)
        {
            query = query.Where(n => n.IsRead == request.IsRead.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : (request.PageSize > 50 ? 50 : request.PageSize);
        var skip = (page - 1) * pageSize;

        var dbItems = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<NotificationDto>();
        foreach (var n in dbItems)
        {
            var resolved = await _targetResolver.ResolveTargetAsync(userId.Value, n.NotificationType, n.RelatedType, n.RelatedId, cancellationToken);
            
            items.Add(new NotificationDto
            {
                NotificationId = n.NotificationId,
                Title = n.Title,
                Message = n.Message,
                NotificationType = n.NotificationType,
                RelatedType = n.RelatedType,
                RelatedId = n.RelatedId,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt,
                TimeAgoText = ComputeTimeAgo(n.CreatedAt),
                TargetUrl = resolved.targetUrl,
                CanOpen = resolved.canOpen,
                DisabledReason = resolved.disabledReason
            });
        }

        return PaginatedResult<NotificationDto>.Create(items, page, pageSize, totalItems);
    }

    private static string ComputeTimeAgo(DateTime createdAt)
    {
        var diff = DateTime.UtcNow - createdAt;
        if (diff.TotalMinutes < 1) return "Vừa xong";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} phút trước";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} giờ trước";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} ngày trước";
        return createdAt.ToString("dd/MM/yyyy");
    }
}

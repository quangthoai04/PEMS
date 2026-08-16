using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Notifications.Common;

using PEMS.Application.Common;
namespace PEMS.Application.Notifications.Queries.GetMyNotifications;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, PaginatedResult<NotificationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyNotificationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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

        var items = dbItems.Select(n => new NotificationDto
        {
            NotificationId = n.NotificationId,
            Title = n.Title,
            Message = n.Message,
            NotificationType = n.NotificationType,
            Category = n.Category,
            Priority = n.Priority.ToString(),
            IsActionRequired = n.IsActionRequired,
            RelatedType = n.RelatedType,
            RelatedId = n.RelatedId,
            VisitRequestId = n.VisitRequestId,
            VisitInstanceId = n.VisitInstanceId,
            CampusId = n.CampusId,
            ActionType = n.ActionType,
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            CreatedAt = n.CreatedAt,
            TimeAgoText = ComputeTimeAgo(n.CreatedAt),
            MetadataJson = n.MetadataJson,
            TargetUrl = n.ActionUrl,
            CanOpen = !string.IsNullOrEmpty(n.ActionUrl),
            DisabledReason = string.IsNullOrEmpty(n.ActionUrl)
                ? "Thông báo này không có đường dẫn chi tiết."
                : null
        }).ToList();

        return PaginatedResult<NotificationDto>.Create(items, page, pageSize, totalItems);
    }

    private static string ComputeTimeAgo(DateTime createdAt)
    {
        var diff = VietnamTime.Now() - createdAt;
        if (diff.TotalMinutes < 1) return "Vừa xong";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} phút trước";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} giờ trước";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} ngày trước";
        return createdAt.ToString("dd/MM/yyyy");
    }
}

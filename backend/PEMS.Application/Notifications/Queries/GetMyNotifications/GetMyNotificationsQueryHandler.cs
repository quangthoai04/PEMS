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

        // Category/action filters must be part of the database query BEFORE Count/Skip/Take.
        // Filtering after pagination creates sparse pages (for example pageSize=10 but only 1-5
        // matching rows are rendered) while totalPages still describes the unfiltered dataset.
        if (request.Categories is { Count: > 0 })
        {
            var categories = request.Categories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (categories.Length > 0)
            {
                query = query.Where(n => categories.Contains(n.Category));
            }
        }

        if (request.IsActionRequired.HasValue)
        {
            query = query.Where(n => n.IsActionRequired == request.IsActionRequired.Value);
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

        var items = dbItems.Select(n =>
        {
            // Approval-required visit notifications used to point at the Visit list. The shared
            // frontend semantic resolver then interpreted OPEN_CAMPUS_REVIEW as a one-shot
            // VISIT_REVIEW command and immediately opened the approve+assign-host modal. The desired
            // notification behavior is safer and clearer: first open the submitted request detail;
            // the reviewer can inspect the request and explicitly choose the approval action there.
            // This compatibility rewrite also fixes notification rows already stored with the old URL.
            var targetUrl = n.ActionType == NotificationActionTypes.OpenCampusReview && n.VisitRequestId.HasValue
                ? $"/dashboard/visit/v2/{n.VisitRequestId.Value}"
                : n.ActionUrl;

            return new NotificationDto
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
                TargetUrl = targetUrl,
                CanOpen = !string.IsNullOrEmpty(targetUrl),
                DisabledReason = string.IsNullOrEmpty(targetUrl)
                    ? "Thông báo này không có đường dẫn chi tiết."
                    : null
            };
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

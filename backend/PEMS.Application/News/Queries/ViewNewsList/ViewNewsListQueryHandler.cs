using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using NewsEntity = PEMS.Domain.Entities.News.News;

namespace PEMS.Application.News.Queries.ViewNewsList;

public sealed class ViewNewsListQueryHandler
    : IRequestHandler<ViewNewsListQuery, ViewNewsListResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ViewNewsListQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ViewNewsListResponse> Handle(
        ViewNewsListQuery request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("You do not have permission to view news.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole = _currentUser.SubRole ?? string.Empty;
        var primaryCampusId = _currentUser.PrimaryCampusId;

        var viewerMode = ResolveViewerMode(roleCode, subRole);
        if (viewerMode is null)
            throw new ForbiddenException("You do not have permission to view news.");

        var keyword = request.Keyword?.Trim();
        var status = request.Status?.Trim();
        var sortDirection = request.SortDirection?.Trim().ToLowerInvariant();

        var query = _dbContext.News.AsNoTracking();

        // AUTHOR mode: Host của chuyến được thấy cả bài của participant cùng chuyến mình host
        // (theo dõi trạng thái duyệt) — ngoài các bài do chính mình viết.
        var hostedInstanceIds = viewerMode == NewsConstants.ViewerMode.Author
            ? await _dbContext.VisitRequestCampuses
                .AsNoTracking()
                .Where(vrc => vrc.CurrentHostUserId == currentUserId)
                .Select(vrc => vrc.VisitInstanceId)
                .ToListAsync(cancellationToken)
            : new List<ulong>();
        var hostedSet = hostedInstanceIds.ToHashSet();

        // Apply role scope
        query = ApplyRoleScope(query, viewerMode, currentUserId, primaryCampusId, hostedInstanceIds);

        // Lọc đúng 1 bài khi đi từ thông báo — đặt sau role scope để không lộ bài ngoài quyền xem.
        if (request.NewsId is { } newsIdFilter)
            query = query.Where(n => n.NewsId == newsIdFilter);

        // Apply status filter
        query = ApplyStatusFilter(query, viewerMode, status);

        // Apply keyword search
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword}%";
            var matchingUserIds = _dbContext.Users
                .Where(u => EF.Functions.Like(u.FullName, pattern))
                .Select(u => u.UserId);

            query = query.Where(n =>
                n.Translations.Any(t =>
                    EF.Functions.Like(t.Title, pattern) ||
                    (t.Summary != null && EF.Functions.Like(t.Summary, pattern))
                )
                || matchingUserIds.Contains(n.AuthorUserId)
                || (n.ReviewedBy != null && matchingUserIds.Contains(n.ReviewedBy.Value))
            );
        }

        var totalItems = await query.CountAsync(cancellationToken);

        // Apply sorting
        var isAsc = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy?.Trim() switch
        {
            "reviewedAt" => isAsc
                ? query.OrderBy(n => n.ReviewedAt).ThenBy(n => n.NewsId)
                : query.OrderByDescending(n => n.ReviewedAt).ThenByDescending(n => n.NewsId),
            _ => isAsc
                ? query.OrderBy(n => n.CreatedAt).ThenBy(n => n.NewsId)
                : query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NewsId)
        };

        var rawItems = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new
            {
                n.NewsId,
                n.CampusId,
                n.VisitInstanceId,
                n.AuthorUserId,
                n.CoverFileId,
                n.Status,
                n.CreatedAt,
                n.UpdatedAt,
                n.ReviewedBy,
                n.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        if (rawItems.Count == 0)
        {
            var canCreateEmpty = await ResolveCanCreateNewsAsync(viewerMode, currentUserId, cancellationToken);
            return new ViewNewsListResponse
            {
                ViewerMode = viewerMode,
                CanCreateNews = canCreateEmpty,
                Items = Array.Empty<ViewNewsListItemDto>(),
                Pagination = BuildPagination(request.Page, request.PageSize, totalItems)
            };
        }

        var newsIds = rawItems.Select(n => n.NewsId).ToList();

        // Batch fetch translations (prefer 'vi', fallback to first)
        var translations = await _dbContext.NewsTranslations
            .AsNoTracking()
            .Where(t => newsIds.Contains(t.NewsId))
            .Select(t => new { t.NewsId, t.LanguageCode, t.Title, t.Summary })
            .ToListAsync(cancellationToken);

        var translationDict = translations
            .GroupBy(t => t.NewsId)
            .ToDictionary(
                g => g.Key,
                g => g.FirstOrDefault(t => t.LanguageCode == "vi") ?? g.First());

        // Batch fetch cover image URLs
        var coverFileIds = rawItems
            .Where(n => n.CoverFileId.HasValue)
            .Select(n => n.CoverFileId!.Value)
            .Distinct()
            .ToList();

        // coverWebUrl[fileId] = webViewUrl, coverThumb[fileId] = thumbnailUrl
        var coverWebUrl = new Dictionary<ulong, string?>();
        var coverThumb = new Dictionary<ulong, string?>();
        if (coverFileIds.Any())
        {
            var coverFiles = await _dbContext.Files
                .AsNoTracking()
                .Where(f => coverFileIds.Contains(f.FileId))
                .Select(f => new { f.FileId, f.WebViewUrl, f.ThumbnailUrl })
                .ToListAsync(cancellationToken);
            foreach (var f in coverFiles)
            {
                coverWebUrl[f.FileId] = f.WebViewUrl;
                coverThumb[f.FileId] = f.ThumbnailUrl;
            }
        }

        // Batch fetch user names (author + reviewer)
        var userIds = rawItems
            .SelectMany(n => new[] { (ulong?)n.AuthorUserId, n.ReviewedBy })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var userNames = userIds.Any()
            ? await _dbContext.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken)
            : new Dictionary<ulong, string>();

        // Batch fetch campus names
        var campusIds = rawItems
            .Where(n => n.CampusId.HasValue)
            .Select(n => n.CampusId!.Value)
            .Distinct()
            .ToList();

        var campusNames = campusIds.Any()
            ? await _dbContext.Campuses
                .AsNoTracking()
                .Where(c => campusIds.Contains(c.CampusId))
                .Select(c => new { c.CampusId, c.Name })
                .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken)
            : new Dictionary<ulong, string>();

        var canCreateNews = await ResolveCanCreateNewsAsync(viewerMode, currentUserId, cancellationToken);

        bool isHoViewer = viewerMode == NewsConstants.ViewerMode.HoReadonly;

        var items = rawItems.Select(n =>
        {
            translationDict.TryGetValue(n.NewsId, out var translation);
            string? coverImageUrl = n.CoverFileId.HasValue && coverWebUrl.TryGetValue(n.CoverFileId.Value, out var wu) ? wu : null;
            string? coverThumbnailUrl = n.CoverFileId.HasValue && coverThumb.TryGetValue(n.CoverFileId.Value, out var tu) ? tu : null;

            userNames.TryGetValue(n.AuthorUserId, out var authorName);
            string? reviewerName = n.ReviewedBy.HasValue && userNames.TryGetValue(n.ReviewedBy.Value, out var rn) ? rn : null;
            string? campusName = n.CampusId.HasValue && campusNames.TryGetValue(n.CampusId.Value, out var cn) ? cn : null;

            return new ViewNewsListItemDto
            {
                NewsId = n.NewsId,
                Title = translation?.Title ?? string.Empty,
                Description = translation?.Summary,
                CoverImageUrl = coverImageUrl,
                CoverThumbnailUrl = coverThumbnailUrl,
                CampusId = n.CampusId,
                CampusName = campusName,
                VisitInstanceId = n.VisitInstanceId,
                AuthorUserId = n.AuthorUserId,
                AuthorName = authorName ?? string.Empty,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                Status = n.Status,
                StatusLabel = NewsConstants.ToVietnameseStatusLabel(n.Status),
                // HO: không trả thông tin reviewer nội bộ.
                ReviewedBy = isHoViewer ? null : n.ReviewedBy,
                ReviewedByName = isHoViewer ? null : reviewerName,
                ReviewedAt = isHoViewer ? null : n.ReviewedAt,
                AvailableActions = BuildActions(
                    viewerMode, n.Status, n.AuthorUserId, currentUserId,
                    n.VisitInstanceId.HasValue && hostedSet.Contains(n.VisitInstanceId.Value))
            };
        }).ToList();

        return new ViewNewsListResponse
        {
            ViewerMode = viewerMode,
            CanCreateNews = canCreateNews,
            Items = items,
            Pagination = BuildPagination(request.Page, request.PageSize, totalItems)
        };
    }

    private static string? ResolveViewerMode(string roleCode, string subRole)
    {
        if (roleCode == RoleCodes.Ho) return NewsConstants.ViewerMode.HoReadonly;
        if (roleCode == RoleCodes.Student) return NewsConstants.ViewerMode.Author;
        if (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff) return NewsConstants.ViewerMode.Author;
        if (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader) return NewsConstants.ViewerMode.Reviewer;
        return null;
    }

    private static IQueryable<NewsEntity> ApplyRoleScope(
        IQueryable<NewsEntity> query,
        string viewerMode,
        ulong currentUserId,
        ulong? primaryCampusId,
        IReadOnlyList<ulong> hostedInstanceIds)
    {
        return viewerMode switch
        {
            // Author: bài của chính mình + (nếu là Host) bài của participant cùng chuyến mình host.
            NewsConstants.ViewerMode.Author =>
                query.Where(n => n.AuthorUserId == currentUserId
                    || (n.VisitInstanceId != null && hostedInstanceIds.Contains(n.VisitInstanceId.Value))),

            NewsConstants.ViewerMode.Reviewer =>
                query.Where(n => n.CampusId == primaryCampusId),

            NewsConstants.ViewerMode.HoReadonly =>
                query.Where(n => n.Status == NewsConstants.Status.Published),

            _ => throw new ForbiddenException("You do not have permission to view news.")
        };
    }

    private static IQueryable<NewsEntity> ApplyStatusFilter(
        IQueryable<NewsEntity> query,
        string viewerMode,
        string? status)
    {
        if (string.IsNullOrWhiteSpace(status) ||
            string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        if (viewerMode == NewsConstants.ViewerMode.HoReadonly &&
            status != NewsConstants.Status.Published)
        {
            throw new ValidationException("Status filter is not allowed for HO.");
        }

        return query.Where(n => n.Status == status);
    }

    private static Task<bool> ResolveCanCreateNewsAsync(
        string viewerMode,
        ulong currentUserId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(viewerMode == NewsConstants.ViewerMode.Author);
    }

    private static NewsAvailableActionsDto BuildActions(
        string viewerMode,
        string status,
        ulong authorUserId,
        ulong currentUserId,
        bool isHostOfInstance)
    {
        if (viewerMode == NewsConstants.ViewerMode.Author)
        {
            var isOwner = authorUserId == currentUserId;
            return new NewsAvailableActionsDto
            {
                // Host xem được bài của participant cùng chuyến — chỉ tác giả được sửa.
                CanViewDetail = isOwner || isHostOfInstance,
                CanEdit = isOwner && (status == NewsConstants.Status.PendingReview || status == NewsConstants.Status.Rejected),
                CanApprove = false,
                CanReject = false,
                CanHide = false,
                CanShow = false
            };
        }

        if (viewerMode == NewsConstants.ViewerMode.Reviewer)
        {
            return new NewsAvailableActionsDto
            {
                CanViewDetail = true,
                CanEdit = false,
                CanApprove = status == NewsConstants.Status.PendingReview,
                CanReject = status == NewsConstants.Status.PendingReview,
                CanHide = status == NewsConstants.Status.Published,
                CanShow = status == NewsConstants.Status.Hidden
            };
        }

        // HO_READONLY
        return new NewsAvailableActionsDto
        {
            CanViewDetail = true,
            CanEdit = false,
            CanApprove = false,
            CanReject = false,
            CanHide = false,
            CanShow = false
        };
    }

    private static NewsPaginationDto BuildPagination(int page, int pageSize, int totalItems)
    {
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize);
        return new NewsPaginationDto
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasPrevious = page > 1,
            HasNext = page < totalPages
        };
    }
}

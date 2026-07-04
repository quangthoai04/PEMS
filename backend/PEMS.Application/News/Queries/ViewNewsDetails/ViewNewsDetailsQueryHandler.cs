using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Queries.ViewNewsDetails;

public sealed class ViewNewsDetailsQueryHandler
    : IRequestHandler<ViewNewsDetailsQuery, ViewNewsDetailsDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ViewNewsDetailsQueryHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ViewNewsDetailsDto> Handle(ViewNewsDetailsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode  = _currentUser.RoleCode ?? string.Empty;
        var subRole   = _currentUser.SubRole  ?? string.Empty;
        var campusId  = _currentUser.PrimaryCampusId;

        // Step 1: Load news
        var news = await _dbContext.News
            .AsNoTracking()
            .Where(n => n.NewsId == request.NewsId)
            .Select(n => new
            {
                n.NewsId, n.CampusId, n.VisitInstanceId, n.AuthorUserId,
                n.CoverFileId, n.Status, n.SubmittedAt,
                n.ReviewedBy, n.ReviewedAt, n.ReviewNote,
                n.PublishedAt, n.RowVersion, n.CreatedAt, n.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tin tức", request.NewsId);

        // Step 2: Authorization scope
        if (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader)
        {
            if (news.CampusId != campusId)
                throw new ForbiddenException("Bạn không có quyền xem bài viết này.");
        }
        else if ((roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
              || roleCode == RoleCodes.Student)
        {
            if (news.AuthorUserId != currentUserId)
                throw new ForbiddenException("Bạn không có quyền xem bài viết này.");
        }
        else if (roleCode == RoleCodes.Ho)
        {
            if (news.Status != NewsConstants.Status.Published)
                throw new ForbiddenException("Bạn không có quyền xem bài viết này.");
        }
        else
        {
            throw new ForbiddenException("Bạn không có quyền xem bài viết này.");
        }

        // Step 3: Translation (requested language → 'vi' → any)
        var translations = await _dbContext.NewsTranslations
            .AsNoTracking()
            .Where(t => t.NewsId == request.NewsId)
            .Select(t => new { t.NewsTranslationId, t.LanguageCode, t.Title, t.Summary, t.Slug })
            .ToListAsync(cancellationToken);

        var requestedLang = string.IsNullOrWhiteSpace(request.LanguageCode)
            ? "vi"
            : request.LanguageCode.Trim();

        var translation = translations.FirstOrDefault(t =>
                              string.Equals(t.LanguageCode, requestedLang, StringComparison.OrdinalIgnoreCase))
                       ?? translations.FirstOrDefault(t => t.LanguageCode == "vi")
                       ?? translations.FirstOrDefault();

        var availableLanguages = translations
            .Select(t => t.LanguageCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l == "vi" ? 0 : 1)
            .ThenBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Step 4: Campus name
        string? campusName = null;
        if (news.CampusId.HasValue)
        {
            campusName = await _dbContext.Campuses
                .AsNoTracking()
                .Where(c => c.CampusId == news.CampusId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Step 5: User names (author + reviewer)
        var userIds = new List<ulong> { news.AuthorUserId };
        if (news.ReviewedBy.HasValue) userIds.Add(news.ReviewedBy.Value);

        var userNames = await _dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName })
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        userNames.TryGetValue(news.AuthorUserId, out var authorName);
        string? reviewerName = news.ReviewedBy.HasValue
            && userNames.TryGetValue(news.ReviewedBy.Value, out var rn) ? rn : null;

        // Step 6: Cover file
        NewsFileDto? coverFile = null;
        if (news.CoverFileId.HasValue)
        {
            coverFile = await _dbContext.Files
                .AsNoTracking()
                .Where(f => f.FileId == news.CoverFileId.Value)
                .Select(f => new NewsFileDto
                {
                    FileId       = f.FileId,
                    Url          = $"/api/files/{f.FileId}/content",
                    ThumbnailUrl = f.ThumbnailUrl,
                    FileName     = f.OriginalFilename,
                    MimeType     = f.MimeType
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Step 7: Sections + section files
        var sections = new List<NewsSectionDto>();
        if (translation != null)
        {
            var rawSections = await _dbContext.NewsContentSections
                .AsNoTracking()
                .Where(s => s.NewsTranslationId == translation.NewsTranslationId)
                .OrderBy(s => s.SectionOrder)
                .Select(s => new
                {
                    s.SectionId, s.SectionOrder,
                    s.SectionTitle, s.SectionBodyHtml, s.SectionBodyText
                })
                .ToListAsync(cancellationToken);

            var sectionIds = rawSections.Select(s => s.SectionId).ToList();

            var rawSectionFiles = sectionIds.Count > 0
                ? await _dbContext.NewsSectionFiles
                    .AsNoTracking()
                    .Where(sf => sectionIds.Contains(sf.SectionId))
                    .Select(sf => new
                    {
                        sf.SectionFileId, sf.SectionId, sf.FileId,
                        sf.UsageType, sf.DisplayOrder,
                        WebViewUrl       = sf.File.WebViewUrl,
                        ThumbnailUrl     = sf.File.ThumbnailUrl,
                        OriginalFilename = sf.File.OriginalFilename,
                        MimeType         = sf.File.MimeType
                    })
                    .ToListAsync(cancellationToken)
                : [];

            var filesBySectionId = rawSectionFiles
                .GroupBy(f => f.SectionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            sections = rawSections.Select(s => new NewsSectionDto
            {
                SectionId       = s.SectionId,
                SectionOrder    = s.SectionOrder,
                SectionTitle    = s.SectionTitle ?? string.Empty,
                SectionBodyHtml = s.SectionBodyHtml ?? string.Empty,
                SectionBodyText = s.SectionBodyText,
                Files = filesBySectionId.TryGetValue(s.SectionId, out var files)
                    ? files.OrderBy(f => f.DisplayOrder).Select(f => new NewsSectionFileDto
                    {
                        SectionFileId = f.SectionFileId,
                        FileId        = f.FileId,
                        Url           = $"/api/files/{f.FileId}/content",
                        ThumbnailUrl  = f.ThumbnailUrl,
                        FileName      = f.OriginalFilename,
                        MimeType      = f.MimeType,
                        UsageType     = f.UsageType,
                        DisplayOrder  = f.DisplayOrder
                    }).ToList()
                    : (IReadOnlyList<NewsSectionFileDto>)Array.Empty<NewsSectionFileDto>()
            }).ToList();
        }

        // Step 8: Build available actions
        var actions = BuildActions(roleCode, subRole, news.Status, news.AuthorUserId, currentUserId);

        return new ViewNewsDetailsDto
        {
            NewsId          = news.NewsId,
            VisitInstanceId = news.VisitInstanceId,
            CampusId        = news.CampusId,
            CampusName      = campusName,
            Status          = news.Status,
            StatusLabel     = NewsConstants.ToVietnameseStatusLabel(news.Status),
            AuthorUserId    = news.AuthorUserId,
            AuthorName      = authorName ?? string.Empty,
            CreatedAt       = news.CreatedAt,
            UpdatedAt       = news.UpdatedAt,
            SubmittedAt     = news.SubmittedAt,
            ReviewedBy      = news.ReviewedBy,
            ReviewedByName  = reviewerName,
            ReviewedAt      = news.ReviewedAt,
            ReviewNote      = news.ReviewNote,
            PublishedAt     = news.PublishedAt,
            RowVersion      = news.RowVersion,
            LanguageCode    = translation?.LanguageCode ?? "vi",
            AvailableLanguages = availableLanguages,
            Title           = translation?.Title ?? string.Empty,
            Summary         = translation?.Summary,
            Slug            = translation?.Slug,
            CoverFile       = coverFile,
            Sections        = sections,
            AvailableActions = actions
        };
    }

    private static NewsDetailAvailableActionsDto BuildActions(
        string roleCode, string subRole, string status,
        ulong authorUserId, ulong currentUserId)
    {
        if (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader)
        {
            return new NewsDetailAvailableActionsDto
            {
                CanViewDetail = true,
                CanEdit       = false,
                CanApprove    = status == NewsConstants.Status.PendingReview,
                CanReject     = status == NewsConstants.Status.PendingReview,
                CanHide       = status == NewsConstants.Status.Published,
                CanShow       = status == NewsConstants.Status.Hidden,
                CanTranslate  = true
            };
        }

        if ((roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
         || roleCode == RoleCodes.Student)
        {
            var isOwner = authorUserId == currentUserId;
            var editableStatus = status == NewsConstants.Status.PendingReview
                              || status == NewsConstants.Status.Rejected;
            return new NewsDetailAvailableActionsDto
            {
                CanViewDetail = isOwner,
                CanEdit       = isOwner && editableStatus,
                CanApprove    = false,
                CanReject     = false,
                CanHide       = false,
                CanShow       = false,
                CanTranslate  = isOwner && editableStatus
            };
        }

        // HO readonly
        return new NewsDetailAvailableActionsDto
        {
            CanViewDetail = true,
            CanEdit       = false,
            CanApprove    = false,
            CanReject     = false,
            CanHide       = false,
            CanShow       = false,
            CanTranslate  = false
        };
    }
}

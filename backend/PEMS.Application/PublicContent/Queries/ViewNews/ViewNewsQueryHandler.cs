using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.ViewNews;

public sealed class ViewNewsQueryHandler : IRequestHandler<ViewNewsQuery, ViewNewsDto>
{
    private readonly IApplicationDbContext _dbContext;

    public ViewNewsQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<ViewNewsDto> Handle(ViewNewsQuery request, CancellationToken cancellationToken)
    {
        var requestedLang = string.IsNullOrWhiteSpace(request.LanguageCode)
            ? "vi"
            : request.LanguageCode.Trim();

        var query = _dbContext.News
            .AsNoTracking()
            .Where(n => n.Status == NewsConstants.Status.Published);

        if (request.IsFeatured.HasValue)
            query = query.Where(n => n.IsFeatured == request.IsFeatured.Value);

        if (request.CampusId.HasValue)
            query = query.Where(n => n.CampusId == request.CampusId.Value);

        switch (request.Type?.Trim().ToLowerInvariant())
        {
            case "featured":
                query = query.Where(n => n.IsFeatured);
                break;
            case "visit":
                query = query.Where(n => n.VisitInstanceId != null);
                break;
            case "general":
                query = query.Where(n => n.VisitInstanceId == null && !n.IsFeatured);
                break;
        }

        var keyword = request.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            // EXISTS over translations — matches the post when any translation matches.
            query = query.Where(n => _dbContext.NewsTranslations.Any(t =>
                t.NewsId == n.NewsId &&
                (t.Title.Contains(keyword) || (t.Summary != null && t.Summary.Contains(keyword)))));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)request.PageSize);
        var pageIndex  = Math.Max(1, request.PageIndex);

        var sortOldest = string.Equals(request.Sort?.Trim(), "oldest", StringComparison.OrdinalIgnoreCase);
        query = sortOldest
            ? query.OrderBy(n => n.PublishedAt).ThenBy(n => n.NewsId)
            : query.OrderByDescending(n => n.PublishedAt).ThenByDescending(n => n.NewsId);

        var pageNews = await query
            .Skip((pageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new
            {
                n.NewsId,
                n.CoverFileId,
                n.PublishedAt,
                n.IsFeatured,
                n.CampusId,
                CampusName = n.CampusId != null ? _dbContext.Campuses.Where(c => c.CampusId == n.CampusId).Select(c => c.Name).FirstOrDefault() : null,
                CampusCode = n.CampusId != null ? _dbContext.Campuses.Where(c => c.CampusId == n.CampusId).Select(c => c.CampusCode).FirstOrDefault() : null,
                IsVisitRelated = n.VisitInstanceId != null
            })
            .ToListAsync(cancellationToken);

        var newsIds = pageNews.Select(n => n.NewsId).ToList();

        var translations = newsIds.Count > 0
            ? await _dbContext.NewsTranslations
                .AsNoTracking()
                .Where(t => newsIds.Contains(t.NewsId))
                .Select(t => new { t.NewsId, t.LanguageCode, t.Title, t.Summary, t.Slug })
                .ToListAsync(cancellationToken)
            : [];

        var translationsByNews = translations
            .GroupBy(t => t.NewsId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = new List<PublicNewsListItemDto>(pageNews.Count);
        foreach (var n in pageNews)
        {
            translationsByNews.TryGetValue(n.NewsId, out var candidates);
            var translation =
                candidates?.FirstOrDefault(t => string.Equals(t.LanguageCode, requestedLang, StringComparison.OrdinalIgnoreCase))
                ?? candidates?.FirstOrDefault(t => t.LanguageCode == "vi")
                ?? candidates?.FirstOrDefault();

            if (translation is null) continue; // no translation at all — nothing renderable

            items.Add(new PublicNewsListItemDto
            {
                NewsId       = n.NewsId,
                Title        = translation.Title,
                Slug         = translation.Slug,
                Summary      = translation.Summary,
                CoverFileId  = n.CoverFileId,
                CoverUrl     = n.CoverFileId.HasValue ? $"/api/public/news-files/{n.CoverFileId.Value}" : null,
                PublishedAt    = n.PublishedAt,
                LanguageCode   = translation.LanguageCode,
                IsFeatured     = n.IsFeatured,
                CampusId       = n.CampusId,
                CampusName     = n.CampusName,
                CampusCode     = n.CampusCode,
                IsVisitRelated = n.IsVisitRelated
            });
        }

        return new ViewNewsDto
        {
            Items      = items,
            PageIndex  = pageIndex,
            PageSize   = request.PageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }
}

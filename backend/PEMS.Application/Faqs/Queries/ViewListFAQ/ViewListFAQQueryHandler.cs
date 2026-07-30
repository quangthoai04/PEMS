using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Faqs;

namespace PEMS.Application.Faqs.Queries.ViewListFAQ;

public sealed class ViewListFAQQueryHandler
    : IRequestHandler<ViewListFAQQuery, PaginatedResult<ViewListFAQDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public ViewListFAQQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedResult<ViewListFAQDto>> Handle(
        ViewListFAQQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page;
        var pageSize = request.PageSize;

        var keyword = request.Keyword is { } kw
            ? System.Text.RegularExpressions.Regex.Replace(kw.Trim(), @"\s+", " ")
            : null;
        if (string.IsNullOrEmpty(keyword)) keyword = null;
        var faqType = request.FaqType?.Trim();
        var status = request.Status?.Trim();
        var sortBy = request.SortBy?.Trim();
        var sortDirection = request.SortDirection?.Trim().ToLowerInvariant();

        var query = _dbContext.Faqs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(faqType) &&
            !string.Equals(faqType, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.FaqType == faqType);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Question, pattern) ||
                EF.Functions.Like(x.Answer, pattern) ||
                EF.Functions.Like(x.FaqType, pattern));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        query = ApplySorting(query, sortBy, sortDirection);

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.FaqId,
                x.FaqType,
                x.Question,
                x.Answer,
                x.DisplayOrder,
                x.Status,
                x.CreatedAt,
                x.CreatedBy,
                x.UpdatedAt,
                x.UpdatedBy
            })
            .ToListAsync(cancellationToken);

        var faqIds = rawItems.Select(x => x.FaqId).ToList();
        var englishByFaqId = await _dbContext.FaqTranslations
            .AsNoTracking()
            .Where(t => faqIds.Contains(t.FaqId) && t.LanguageCode == "en")
            .Select(t => new { t.FaqId, t.Question, t.Answer })
            .ToDictionaryAsync(t => t.FaqId, cancellationToken);

        // Collect user IDs to resolve names in one query
        var userIds = rawItems
            .SelectMany(x => new[] { x.CreatedBy, x.UpdatedBy })
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

        var items = rawItems
            .Select(x => new ViewListFAQDto
            {
                FaqId = x.FaqId,
                FaqType = x.FaqType,
                FaqTypeLabel = FaqConstants.ToVietnameseTypeLabel(x.FaqType),
                Question = x.Question,
                Answer = x.Answer,
                EnglishQuestion = englishByFaqId.TryGetValue(x.FaqId, out var en) ? en.Question : null,
                EnglishAnswer = englishByFaqId.TryGetValue(x.FaqId, out var en2) ? en2.Answer : null,
                HasEnglishTranslation = englishByFaqId.ContainsKey(x.FaqId),
                DisplayOrder = x.DisplayOrder,
                Status = x.Status,
                StatusLabel = FaqConstants.ToVietnameseStatusLabel(x.Status),
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy,
                CreatedByName = x.CreatedBy.HasValue && userNames.TryGetValue(x.CreatedBy.Value, out var cn) ? cn : null,
                UpdatedAt = x.UpdatedAt,
                UpdatedBy = x.UpdatedBy,
                UpdatedByName = x.UpdatedBy.HasValue && userNames.TryGetValue(x.UpdatedBy.Value, out var un) ? un : null
            })
            .ToList();

        return PaginatedResult<ViewListFAQDto>.Create(items, page, pageSize, totalItems);
    }

    private static IQueryable<Faq> ApplySorting(
        IQueryable<Faq> query,
        string? sortBy,
        string? sortDirection)
    {
        var isAsc = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return sortBy switch
        {
            "displayOrder" => isAsc
                ? query.OrderBy(x => x.DisplayOrder).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.FaqId)
                : query.OrderByDescending(x => x.DisplayOrder).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.FaqId),

            _ => isAsc
                ? query.OrderBy(x => x.CreatedAt).ThenBy(x => x.FaqId)
                : query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.FaqId)
        };
    }
}

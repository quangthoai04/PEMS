using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.PublicContent.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.ViewFAQ;

public sealed class ViewFaqQueryHandler : IRequestHandler<ViewFaqQuery, PaginatedResult<ViewFaqDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IFaqTranslationCache _translationCache;

    public ViewFaqQueryHandler(IApplicationDbContext dbContext, IFaqTranslationCache translationCache)
    {
        _dbContext = dbContext;
        _translationCache = translationCache;
    }

    public async Task<PaginatedResult<ViewFaqDto>> Handle(
        ViewFaqQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page;
        var pageSize = request.PageSize;
        var keyword = request.Keyword?.Trim();
        var faqType = request.FaqType?.Trim();

        var query = _dbContext.Faqs
            .AsNoTracking()
            .Where(x => x.Status == "PUBLISHED");

        if (!string.IsNullOrWhiteSpace(faqType) &&
            !string.Equals(faqType, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.FaqType == faqType);
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

        var rawItems = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.FaqId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.FaqId,
                x.FaqType,
                x.Question,
                x.Answer,
                x.DisplayOrder,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        // Question/answer are Vietnamese-only in the DB by design; translate + cache for any
        // other requested language (falls back to the Vietnamese text for "vi"/unsupported).
        var translations = await _translationCache.TranslateAsync(
            rawItems.Select(x => new FaqTranslationSource(x.FaqId, x.Question, x.Answer, x.UpdatedAt)).ToList(),
            request.LanguageCode,
            cancellationToken);

        var items = rawItems
            .Select(x =>
            {
                translations.TryGetValue(x.FaqId, out var translated);
                return new ViewFaqDto
                {
                    FaqId = x.FaqId,
                    FaqType = x.FaqType,
                    FaqTypeLabel = FaqConstants.ToTypeLabel(x.FaqType, request.LanguageCode),
                    Question = translated.Question ?? x.Question,
                    Answer = translated.Answer ?? x.Answer,
                    DisplayOrder = x.DisplayOrder,
                    CreatedAt = x.CreatedAt
                };
            })
            .ToList();

        return PaginatedResult<ViewFaqDto>.Create(items, page, pageSize, totalItems);
    }
}

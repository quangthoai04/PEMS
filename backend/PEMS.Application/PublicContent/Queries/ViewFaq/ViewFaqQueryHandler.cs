using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Faqs;

namespace PEMS.Application.PublicContent.Queries.ViewFAQ;

/// <summary>
/// Public FAQ list — reads pre-translated VI/EN content straight from <c>faq_translations</c>.
/// Never calls a translation API: content is translated once, at FAQ create/update time
/// (see CreateFAQCommandHandler/UpdateFAQCommandHandler), and simply falls back
/// requested language → 'vi' → the legacy Vietnamese columns on <c>faqs</c> itself for any row
/// that somehow predates the translation table being populated.
/// </summary>
public sealed class ViewFaqQueryHandler : IRequestHandler<ViewFaqQuery, PaginatedResult<ViewFaqDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public ViewFaqQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaginatedResult<ViewFaqDto>> Handle(
        ViewFaqQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page;
        var pageSize = request.PageSize;
        var keyword = request.Keyword?.Trim();
        var faqType = request.FaqType?.Trim();
        var requestedLang = string.IsNullOrWhiteSpace(request.LanguageCode)
            ? NewsConstants.Languages.Default
            : request.LanguageCode.Trim().ToLowerInvariant();

        var query = _dbContext.Faqs
            .AsNoTracking()
            .Where(x => x.Status == "PUBLISHED");

        if (!string.IsNullOrWhiteSpace(faqType) &&
            !string.Equals(faqType, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.FaqType == faqType);
        }

        var rawItems = await query
            .Select(x => new
            {
                x.FaqId,
                x.FaqType,
                x.Question,
                x.Answer,
                x.DisplayOrder,
                x.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var faqIds = rawItems.Select(x => x.FaqId).ToList();
        var translations = faqIds.Count == 0
            ? new List<FaqTranslation>()
            : await _dbContext.FaqTranslations
                .AsNoTracking()
                .Where(t => faqIds.Contains(t.FaqId) && (t.LanguageCode == requestedLang || t.LanguageCode == "vi"))
                .ToListAsync(cancellationToken);

        var translationsByFaqId = translations
            .GroupBy(t => t.FaqId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // requested language → 'vi' → the legacy columns on `faqs` itself.
        var displayed = rawItems.Select(x =>
        {
            var rows = translationsByFaqId.GetValueOrDefault(x.FaqId);
            var chosen = rows?.FirstOrDefault(t => t.LanguageCode == requestedLang)
                       ?? rows?.FirstOrDefault(t => t.LanguageCode == "vi");
            return new
            {
                x.FaqId,
                x.FaqType,
                Question = chosen?.Question ?? x.Question,
                Answer = chosen?.Answer ?? x.Answer,
                x.DisplayOrder,
                x.CreatedAt,
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            displayed = displayed.Where(x =>
                x.Question.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.Answer.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.FaqType.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        var totalItems = displayed.Count;

        var items = displayed
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.FaqId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ViewFaqDto
            {
                FaqId = x.FaqId,
                FaqType = x.FaqType,
                FaqTypeLabel = FaqConstants.ToTypeLabel(x.FaqType, requestedLang),
                Question = x.Question,
                Answer = x.Answer,
                DisplayOrder = x.DisplayOrder,
                CreatedAt = x.CreatedAt
            })
            .ToList();

        return PaginatedResult<ViewFaqDto>.Create(items, page, pageSize, totalItems);
    }
}

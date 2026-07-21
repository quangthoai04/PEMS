using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.PublicContent.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

public sealed class SearchInformationQueryHandler : IRequestHandler<SearchInformationQuery, SearchInformationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFaqTranslationCache _faqTranslationCache;

    public SearchInformationQueryHandler(IApplicationDbContext db, IFaqTranslationCache faqTranslationCache)
    {
        _db = db;
        _faqTranslationCache = faqTranslationCache;
    }

    public async Task<SearchInformationDto> Handle(SearchInformationQuery request, CancellationToken cancellationToken)
    {
        var keyword = request.Keyword?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            return new SearchInformationDto();
        }

        var pattern = $"%{keyword}%";
        var limit = request.Limit;
        var requestedLang = string.IsNullOrWhiteSpace(request.LanguageCode)
            ? NewsConstants.Languages.Default
            : request.LanguageCode!.Trim();

        // Run sequentially — a single DbContext instance cannot execute concurrent queries.

        // News — PUBLISHED only, matched on any translation; result text prefers the requested
        // language and falls back to 'vi' — same convention as ViewNewsQueryHandler.
        var rawNews = await _db.News
            .AsNoTracking()
            .Where(n => n.Status == NewsConstants.Status.Published)
            .Where(n => _db.NewsTranslations.Any(t =>
                t.NewsId == n.NewsId &&
                (EF.Functions.Like(t.Title, pattern) || (t.Summary != null && EF.Functions.Like(t.Summary, pattern)))))
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .Select(n => new
            {
                n.NewsId,
                n.PublishedAt,
                Translation = _db.NewsTranslations
                    .Where(t => t.NewsId == n.NewsId)
                    .OrderBy(t => t.LanguageCode == requestedLang ? 0 : (t.LanguageCode == "vi" ? 1 : 2))
                    .Select(t => new { t.Title, t.Summary })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // Partners — APPROVED + PUBLIC only, matched on name/shortName/description.
        var partners = await _db.Partners
            .AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public)
            .Where(p => EF.Functions.Like(p.Name, pattern)
                        || (p.ShortName != null && EF.Functions.Like(p.ShortName, pattern))
                        || (p.Description != null && EF.Functions.Like(p.Description, pattern)))
            .OrderBy(p => p.Name)
            .Take(limit)
            .Select(p => new SearchPartnerResultDto
            {
                PartnerId = p.PartnerId,
                Name = p.Name,
                Country = p.Country,
                PublicSlug = p.PublicSlug,
            })
            .ToListAsync(cancellationToken);

        // FAQs — PUBLISHED only, matched on question/answer (same contract as ViewFaqQueryHandler).
        // Question text (and type label) prefer the requested language, translated + cached —
        // faqs.question/answer are Vietnamese-only in the DB by design.
        var rawFaqs = await _db.Faqs
            .AsNoTracking()
            .Where(f => f.Status == FaqConstants.Status.Published)
            .Where(f => EF.Functions.Like(f.Question, pattern) || EF.Functions.Like(f.Answer, pattern))
            .OrderBy(f => f.DisplayOrder)
            .Take(limit)
            .Select(f => new { f.FaqId, f.FaqType, f.Question, f.Answer, f.UpdatedAt })
            .ToListAsync(cancellationToken);

        var faqTranslations = await _faqTranslationCache.TranslateAsync(
            rawFaqs.Select(f => new FaqTranslationSource(f.FaqId, f.Question, f.Answer, f.UpdatedAt)).ToList(),
            requestedLang,
            cancellationToken);

        var faqs = rawFaqs
            .Select(f =>
            {
                faqTranslations.TryGetValue(f.FaqId, out var translated);
                return new SearchFaqResultDto
                {
                    FaqId = f.FaqId,
                    Question = translated.Question ?? f.Question,
                    FaqTypeLabel = FaqConstants.ToTypeLabel(f.FaqType, requestedLang),
                };
            })
            .ToList();

        // Campuses — ACTIVE only, matched on name/code/city (same visibility as GetActiveCampusesQuery).
        var campuses = await _db.Campuses
            .AsNoTracking()
            .Where(c => c.Status == "ACTIVE")
            .Where(c => EF.Functions.Like(c.Name, pattern)
                        || EF.Functions.Like(c.CampusCode, pattern)
                        || (c.City != null && EF.Functions.Like(c.City, pattern)))
            .OrderBy(c => c.CampusCode)
            .Take(limit)
            .Select(c => new SearchCampusResultDto
            {
                CampusId = c.CampusId,
                CampusCode = c.CampusCode,
                Name = c.Name,
            })
            .ToListAsync(cancellationToken);

        var news = rawNews
            .Where(n => n.Translation is not null)
            .Select(n => new SearchNewsResultDto
            {
                NewsId = n.NewsId,
                Title = n.Translation!.Title,
                Summary = n.Translation!.Summary,
                PublishedAt = n.PublishedAt,
            })
            .ToList();

        return new SearchInformationDto
        {
            News = news,
            Partners = partners,
            Faqs = faqs,
            Campuses = campuses,
            TotalCount = news.Count + partners.Count + faqs.Count + campuses.Count,
        };
    }
}

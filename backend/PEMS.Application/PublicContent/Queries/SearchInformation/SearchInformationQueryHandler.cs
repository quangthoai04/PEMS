using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Faqs;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

/// <summary>
/// Reads pre-translated VI/EN content straight from <c>news_translations</c> /
/// <c>partner_translations</c> / <c>faq_translations</c> — never calls a translation API. Each
/// section is matched and displayed in the requested language, falling back to 'vi' (same
/// convention as ViewNewsQueryHandler/ViewFaqQueryHandler/GetPublicPartnersQueryHandler).
/// </summary>
public sealed class SearchInformationQueryHandler : IRequestHandler<SearchInformationQuery, SearchInformationDto>
{
    private readonly IApplicationDbContext _db;

    public SearchInformationQueryHandler(IApplicationDbContext db)
    {
        _db = db;
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
            : request.LanguageCode!.Trim().ToLowerInvariant();

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

        // Partners — APPROVED + PUBLIC only, matched/displayed on the requested language's
        // translated content (falls back to 'vi' then the legacy columns on `partners`).
        var candidatePartners = await _db.Partners
            .AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public)
            .Select(p => new { p.PartnerId, p.Name, p.ShortName, p.Description, p.Country, p.PublicSlug })
            .ToListAsync(cancellationToken);

        var partnerIds = candidatePartners.Select(p => p.PartnerId).ToList();
        var partnerTranslations = partnerIds.Count == 0
            ? new List<PartnerTranslation>()
            : await _db.PartnerTranslations
                .AsNoTracking()
                .Where(t => partnerIds.Contains(t.PartnerId) && (t.LanguageCode == requestedLang || t.LanguageCode == "vi"))
                .ToListAsync(cancellationToken);
        var partnerTranslationsByPartnerId = partnerTranslations
            .GroupBy(t => t.PartnerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var partners = candidatePartners
            .Select(p =>
            {
                var trs = partnerTranslationsByPartnerId.GetValueOrDefault(p.PartnerId);
                var chosen = trs?.FirstOrDefault(t => t.LanguageCode == requestedLang)
                           ?? trs?.FirstOrDefault(t => t.LanguageCode == "vi");
                return new
                {
                    p.PartnerId,
                    Name = chosen?.Name ?? p.Name,
                    ShortName = chosen?.ShortName ?? p.ShortName,
                    Description = chosen?.Description ?? p.Description,
                    Country = chosen?.Country ?? p.Country,
                    p.PublicSlug,
                };
            })
            .Where(p =>
                p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (p.ShortName is not null && p.ShortName.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                (p.Description is not null && p.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.Name)
            .Take(limit)
            .Select(p => new SearchPartnerResultDto
            {
                PartnerId = p.PartnerId,
                Name = p.Name,
                Country = p.Country,
                PublicSlug = p.PublicSlug,
            })
            .ToList();

        // FAQs — PUBLISHED only, matched/displayed on the requested language's translated content
        // (falls back to 'vi' then the legacy columns on `faqs`).
        var candidateFaqs = await _db.Faqs
            .AsNoTracking()
            .Where(f => f.Status == FaqConstants.Status.Published)
            .Select(f => new { f.FaqId, f.FaqType, f.Question, f.Answer, f.DisplayOrder })
            .ToListAsync(cancellationToken);

        var faqIds = candidateFaqs.Select(f => f.FaqId).ToList();
        var faqTranslations = faqIds.Count == 0
            ? new List<FaqTranslation>()
            : await _db.FaqTranslations
                .AsNoTracking()
                .Where(t => faqIds.Contains(t.FaqId) && (t.LanguageCode == requestedLang || t.LanguageCode == "vi"))
                .ToListAsync(cancellationToken);
        var faqTranslationsByFaqId = faqTranslations
            .GroupBy(t => t.FaqId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var faqs = candidateFaqs
            .Select(f =>
            {
                var trs = faqTranslationsByFaqId.GetValueOrDefault(f.FaqId);
                var chosen = trs?.FirstOrDefault(t => t.LanguageCode == requestedLang)
                           ?? trs?.FirstOrDefault(t => t.LanguageCode == "vi");
                return new
                {
                    f.FaqId,
                    f.FaqType,
                    Question = chosen?.Question ?? f.Question,
                    Answer = chosen?.Answer ?? f.Answer,
                    f.DisplayOrder,
                };
            })
            .Where(f =>
                f.Question.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                f.Answer.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.DisplayOrder)
            .Take(limit)
            .Select(f => new SearchFaqResultDto
            {
                FaqId = f.FaqId,
                Question = f.Question,
                FaqTypeLabel = FaqConstants.ToTypeLabel(f.FaqType, requestedLang),
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

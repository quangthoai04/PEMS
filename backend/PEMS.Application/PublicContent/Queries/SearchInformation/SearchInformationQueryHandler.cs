using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Application.Galleries.Public.Common;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

/// <summary>
/// Site-wide public search over News / Partners / Gallery / FAQ.
///
/// <para><b>One language, end to end.</b> Matching and display both happen in the requested language.
/// An EN search reads EN translations only: a row whose EN text is missing (or, for gallery, not yet
/// READY) simply does not exist for that search — it is never matched on Vietnamese and shown in
/// English. VI keeps the established public-endpoint fallback (VI translation → the legacy Vietnamese
/// columns), because those columns ARE the Vietnamese content. Nothing here calls a translation API;
/// it only reads what was translated at author time.</para>
///
/// <para><b>Pushed to the database.</b> Visibility, language, keyword match, ranking and
/// <c>Take(limit + 1)</c> all run in SQL — no section loads its table into memory to filter it. Keyword
/// matching uses <c>ToLower().Contains(...)</c> rather than a hand-built LIKE pattern: it translates to
/// <c>LOWER(col) LIKE '%kw%'</c>, is case-insensitive on every provider, and — unlike an interpolated
/// pattern — treats <c>%</c> and <c>_</c> typed by the user as literal characters instead of wildcards.</para>
/// </summary>
public sealed class SearchInformationQueryHandler : IRequestHandler<SearchInformationQuery, SearchInformationDto>
{
    private readonly IApplicationDbContext _db;

    /// <summary>Cut length for the secondary text shown under a result row.</summary>
    private const int PreviewMaxLength = 160;

    // Relevance tiers (§15): primary field exact > starts-with > contains > secondary text > metadata.
    private const int ScoreExact = 100;
    private const int ScoreStartsWith = 80;
    private const int ScoreContains = 60;
    private const int ScoreSecondary = 40;
    private const int ScoreMetadata = 20;

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

        var kw = keyword.ToLowerInvariant();
        var limit = request.Limit;
        var probe = limit + 1; // one extra row per section is the whole hasMore mechanism (§16)
        var lang = SearchLanguages.Normalize(request.LanguageCode);
        var isEnglish = lang == SearchLanguages.English;

        // Sequential by necessity: a single DbContext cannot run concurrent queries.
        var newsRows = await BuildNewsQuery(_db, kw, lang, probe).ToListAsync(cancellationToken);
        var partnerRows = await BuildPartnerQuery(_db, kw, lang, isEnglish, probe).ToListAsync(cancellationToken);
        var galleryRows = await BuildGalleryQuery(_db, kw, isEnglish, probe).ToListAsync(cancellationToken);
        var faqRows = await BuildFaqQuery(_db, kw, lang, isEnglish, probe).ToListAsync(cancellationToken);

        var news = newsRows.Take(limit).Select(n => new SearchNewsResultDto
        {
            NewsId = n.NewsId,
            Title = n.Title,
            Summary = Preview(n.Summary),
            PublishedAt = n.PublishedAt,
        }).ToList();

        var partners = partnerRows.Take(limit).Select(p => new SearchPartnerResultDto
        {
            PartnerId = p.PartnerId,
            Name = p.Name!,
            DescriptionPreview = Preview(p.Description),
            Country = p.Country,
            PublicSlug = p.PublicSlug,
        }).ToList();

        var galleries = await BuildGalleryResultsAsync(galleryRows.Take(limit).ToList(), cancellationToken);

        var faqs = faqRows.Take(limit).Select(f => new SearchFaqResultDto
        {
            FaqId = f.FaqId,
            Question = f.Question!,
            AnswerPreview = Preview(f.Answer),
            FaqType = f.FaqType,
            FaqTypeLabel = FaqConstants.ToTypeLabel(f.FaqType, lang),
        }).ToList();

        return new SearchInformationDto
        {
            News = news,
            Partners = partners,
            Galleries = galleries,
            Faqs = faqs,
            HasMore = new SearchHasMoreDto
            {
                News = newsRows.Count > limit,
                Partners = partnerRows.Count > limit,
                Galleries = galleryRows.Count > limit,
                Faqs = faqRows.Count > limit,
            },
            // Rows actually returned — see SearchInformationDto.TotalCount; no COUNT(*) is run.
            TotalCount = news.Count + partners.Count + galleries.Count + faqs.Count,
        };
    }

    // ── News ────────────────────────────────────────────────────────────────────────────────────
    // PUBLISHED only. Title/summary live exclusively in news_translations, so "has content in this
    // language" and "matches in this language" are the same single filter on LanguageCode == lang.
    // internal (not private) so PEMS.IntegrationTests can call ToQueryString() on the composed query and
    // assert it really is SQL — the InMemory unit tests prove the semantics, that test proves the
    // pushdown, and neither alone would.
    internal static IQueryable<NewsRow> BuildNewsQuery(
        IApplicationDbContext db, string kw, string lang, int probe) =>
        db.News
            .AsNoTracking()
            .Where(n => n.Status == NewsConstants.Status.Published)
            .SelectMany(
                n => db.NewsTranslations.Where(t => t.NewsId == n.NewsId && t.LanguageCode == lang),
                (n, t) => new NewsRow
                {
                    NewsId = n.NewsId,
                    PublishedAt = n.PublishedAt,
                    Title = t.Title,
                    Summary = t.Summary,
                    Score =
                        t.Title.ToLower() == kw ? ScoreExact
                        : t.Title.ToLower().StartsWith(kw) ? ScoreStartsWith
                        : t.Title.ToLower().Contains(kw) ? ScoreContains
                        : ScoreSecondary,
                })
            .Where(r => r.Title.ToLower().Contains(kw)
                        || (r.Summary != null && r.Summary.ToLower().Contains(kw)))
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.PublishedAt)
            .ThenByDescending(r => r.NewsId)
            .Take(probe);

    // ── Partners ────────────────────────────────────────────────────────────────────────────────
    // APPROVED + PUBLIC only. EN takes the EN translation and nothing else; VI takes the VI
    // translation and falls back to the legacy Vietnamese columns on `partners`.
    internal static IQueryable<PartnerRow> BuildPartnerQuery(
        IApplicationDbContext db, string kw, string lang, bool isEnglish, int probe) =>
        db.Partners
            .AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public)
            .Select(p => new PartnerRow
            {
                PartnerId = p.PartnerId,
                PublicSlug = p.PublicSlug,
                Name = db.PartnerTranslations
                          .Where(t => t.PartnerId == p.PartnerId && t.LanguageCode == lang)
                          .Select(t => (string?)t.Name).FirstOrDefault()
                       ?? (isEnglish ? null : p.Name),
                ShortName = db.PartnerTranslations
                               .Where(t => t.PartnerId == p.PartnerId && t.LanguageCode == lang)
                               .Select(t => t.ShortName).FirstOrDefault()
                            ?? (isEnglish ? null : p.ShortName),
                Description = db.PartnerTranslations
                                 .Where(t => t.PartnerId == p.PartnerId && t.LanguageCode == lang)
                                 .Select(t => t.Description).FirstOrDefault()
                              ?? (isEnglish ? null : p.Description),
                Country = db.PartnerTranslations
                             .Where(t => t.PartnerId == p.PartnerId && t.LanguageCode == lang)
                             .Select(t => t.Country).FirstOrDefault()
                          ?? (isEnglish ? null : p.Country),
            })
            // Name null ⇒ no content in this language ⇒ invisible to this search (strict EN rule).
            .Where(r => r.Name != null &&
                        (r.Name.ToLower().Contains(kw)
                         || (r.ShortName != null && r.ShortName.ToLower().Contains(kw))
                         || (r.Description != null && r.Description.ToLower().Contains(kw))
                         || (r.Country != null && r.Country.ToLower().Contains(kw))))
            .Select(r => new PartnerRow
            {
                PartnerId = r.PartnerId,
                PublicSlug = r.PublicSlug,
                Name = r.Name,
                ShortName = r.ShortName,
                Description = r.Description,
                Country = r.Country,
                Score =
                    r.Name!.ToLower() == kw ? ScoreExact
                    : r.Name.ToLower().StartsWith(kw) ? ScoreStartsWith
                    : r.Name.ToLower().Contains(kw) ? ScoreContains
                    : (r.ShortName != null && r.ShortName.ToLower().Contains(kw)) ? ScoreContains
                    : (r.Description != null && r.Description.ToLower().Contains(kw)) ? ScoreSecondary
                    : ScoreMetadata,
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Name)
            .ThenBy(r => r.PartnerId)
            .Take(probe);

    // ── FAQs ────────────────────────────────────────────────────────────────────────────────────
    // PUBLISHED only. Same language rule as partners: EN strictly from faq_translations, VI with the
    // legacy Vietnamese columns on `faqs` as fallback.
    internal static IQueryable<FaqRow> BuildFaqQuery(
        IApplicationDbContext db, string kw, string lang, bool isEnglish, int probe) =>
        db.Faqs
            .AsNoTracking()
            .Where(f => f.Status == FaqConstants.Status.Published)
            .Select(f => new FaqRow
            {
                FaqId = f.FaqId,
                FaqType = f.FaqType,
                DisplayOrder = f.DisplayOrder,
                Question = db.FaqTranslations
                              .Where(t => t.FaqId == f.FaqId && t.LanguageCode == lang)
                              .Select(t => (string?)t.Question).FirstOrDefault()
                           ?? (isEnglish ? null : f.Question),
                Answer = db.FaqTranslations
                            .Where(t => t.FaqId == f.FaqId && t.LanguageCode == lang)
                            .Select(t => (string?)t.Answer).FirstOrDefault()
                         ?? (isEnglish ? null : f.Answer),
            })
            .Where(r => r.Question != null &&
                        (r.Question.ToLower().Contains(kw)
                         || (r.Answer != null && r.Answer.ToLower().Contains(kw))))
            .Select(r => new FaqRow
            {
                FaqId = r.FaqId,
                FaqType = r.FaqType,
                DisplayOrder = r.DisplayOrder,
                Question = r.Question,
                Answer = r.Answer,
                Score =
                    r.Question!.ToLower() == kw ? ScoreExact
                    : r.Question.ToLower().StartsWith(kw) ? ScoreStartsWith
                    : r.Question.ToLower().Contains(kw) ? ScoreContains
                    : ScoreSecondary,
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.DisplayOrder)
            .ThenBy(r => r.FaqId)
            .Take(probe);

    // ── Gallery ─────────────────────────────────────────────────────────────────────────────────
    // The visibility chain is copied field-for-field from GetPublicGalleryItemDetailQueryHandler /
    // GetPublicLocationGalleryItemsQueryHandler (item PUBLISHED & not deleted, location/area/campus
    // ACTIVE, ≥1 ACTIVE media). It has to be identical: anything looser would surface a result whose
    // detail call then 404s on click.
    //
    // EN uses the gated EN columns — title/area/location are only real English once their own
    // TranslationStatus is READY (PublicGalleryTranslation.EnOrNull's rule, inlined here so it can run
    // in SQL); the EN description is author-entered, so it is gated on non-blank alone, exactly as the
    // public grid handler does.
    internal static IQueryable<GalleryRow> BuildGalleryQuery(
        IApplicationDbContext db, string kw, bool isEnglish, int probe)
    {
        const string ready = GalleryTranslationStatuses.Ready;

        return db.GalleryItems
            .AsNoTracking()
            .Where(i =>
                i.Status == "PUBLISHED" &&
                i.DeletedAt == null &&
                i.Location.Status == "ACTIVE" &&
                i.Location.Area.Status == "ACTIVE" &&
                i.Location.Area.Campus.Status == "ACTIVE" &&
                i.Media.Any(m => m.Status == "ACTIVE" && m.DeletedAt == null))
            .Select(i => new GalleryRow
            {
                GalleryItemId = i.GalleryItemId,
                DisplayOrder = i.DisplayOrder,
                MediaKind = i.MediaKind,
                CampusCode = i.Location.Area.Campus.CampusCode,
                CampusName = i.Location.Area.Campus.Name,
                City = i.Location.Area.Campus.City,
                AreaId = i.Location.AreaId,
                LocationId = i.LocationId,
                Title = isEnglish
                    ? (i.TranslationStatus == ready ? i.TitleEn : null)
                    : i.Title,
                Description = isEnglish
                    ? (i.Content != null ? i.Content.DescriptionEn : null)
                    : (i.Content != null ? i.Content.DescriptionVi : null),
                AreaName = isEnglish
                    ? (i.Location.Area.TranslationStatus == ready ? i.Location.Area.AreaNameEn : null)
                    : i.Location.Area.AreaName,
                LocationName = isEnglish
                    ? (i.Location.TranslationStatus == ready ? i.Location.LocationNameEn : null)
                    : i.Location.LocationName,
            })
            // No usable title in this language ⇒ not part of this language's search (§10).
            .Where(r => r.Title != null && r.Title != "" &&
                        (r.Title.ToLower().Contains(kw)
                         || (r.Description != null && r.Description.ToLower().Contains(kw))
                         || (r.AreaName != null && r.AreaName.ToLower().Contains(kw))
                         || (r.LocationName != null && r.LocationName.ToLower().Contains(kw))
                         || r.CampusCode.ToLower().Contains(kw)
                         // Campus name/city are Vietnamese master data — metadata, matched in VI mode only.
                         || (!isEnglish && r.CampusName.ToLower().Contains(kw))
                         || (!isEnglish && r.City != null && r.City.ToLower().Contains(kw))))
            .Select(r => new GalleryRow
            {
                GalleryItemId = r.GalleryItemId,
                DisplayOrder = r.DisplayOrder,
                MediaKind = r.MediaKind,
                CampusCode = r.CampusCode,
                CampusName = r.CampusName,
                City = r.City,
                AreaId = r.AreaId,
                LocationId = r.LocationId,
                Title = r.Title,
                Description = r.Description,
                AreaName = r.AreaName,
                LocationName = r.LocationName,
                Score =
                    r.Title!.ToLower() == kw ? ScoreExact
                    : r.Title.ToLower().StartsWith(kw) ? ScoreStartsWith
                    : r.Title.ToLower().Contains(kw) ? ScoreContains
                    : (r.Description != null && r.Description.ToLower().Contains(kw)) ? ScoreSecondary
                    : ScoreMetadata,
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.DisplayOrder)
            .ThenBy(r => r.GalleryItemId)
            .Take(probe);
    }

    /// <summary>
    /// Attaches the public thumbnail of each item's primary media (same primary-selection and URL rules
    /// as the public grid: IsPrimary first, then display order, then media id; URLs built by
    /// <see cref="PublicGalleryMediaFactory"/> so YouTube items get YouTube's thumbnail and uploaded
    /// files get the anonymous proxy route — never an internal Drive URL). One extra query over at most
    /// <c>limit</c> items.
    /// </summary>
    private async Task<List<SearchGalleryResultDto>> BuildGalleryResultsAsync(
        List<GalleryRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return new List<SearchGalleryResultDto>();
        }

        var itemIds = rows.Select(r => r.GalleryItemId).ToList();
        var mediaRaw = await _db.GalleryItemMedia
            .AsNoTracking()
            .Where(m => itemIds.Contains(m.GalleryItemId) && m.Status == "ACTIVE" && m.DeletedAt == null)
            .Select(m => new
            {
                m.GalleryItemId,
                m.MediaId,
                m.FileId,
                m.MediaType,
                m.ThumbnailFileId,
                m.Caption,
                m.AltText,
                m.IsPrimary,
                m.DisplayOrder,
                FilePurpose = m.File.FilePurpose,
                ExternalFileId = m.File.ExternalFileId,
                WebViewUrl = m.File.WebViewUrl,
                FileThumbnailUrl = m.File.ThumbnailUrl,
            })
            .ToListAsync(cancellationToken);

        var thumbnailByItem = mediaRaw
            .GroupBy(m => m.GalleryItemId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var primary = g
                        .OrderByDescending(m => m.IsPrimary)
                        .ThenBy(m => m.DisplayOrder)
                        .ThenBy(m => m.MediaId)
                        .First();
                    var media = PublicGalleryMediaFactory.Build(
                        primary.MediaId, primary.FileId, primary.MediaType, primary.ThumbnailFileId,
                        primary.Caption, primary.AltText, primary.IsPrimary, (int)primary.DisplayOrder,
                        primary.FilePurpose, primary.ExternalFileId, primary.WebViewUrl,
                        primary.FileThumbnailUrl);
                    // Prefer the real thumbnail; fall back to the image itself for items without one.
                    return media.ThumbnailUrl ?? media.Url;
                });

        return rows.Select(r => new SearchGalleryResultDto
        {
            GalleryItemId = r.GalleryItemId,
            Title = r.Title!,
            DescriptionPreview = Preview(r.Description),
            CampusCode = r.CampusCode,
            CampusName = r.CampusName,
            AreaId = r.AreaId,
            AreaName = r.AreaName ?? string.Empty,
            LocationId = r.LocationId,
            LocationName = r.LocationName ?? string.Empty,
            MediaKind = r.MediaKind,
            ThumbnailUrl = thumbnailByItem.GetValueOrDefault(r.GalleryItemId),
        }).ToList();
    }

    /// <summary>Single-line, length-capped secondary text for a result row.</summary>
    private static string? Preview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var flat = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return flat.Length <= PreviewMaxLength
            ? flat
            : flat.Substring(0, PreviewMaxLength).TrimEnd() + "…";
    }

    // Projection shapes. Classes (not anonymous types) so the same shape can be re-projected when the
    // relevance score is added on top of the language-resolved fields.
    internal sealed class NewsRow
    {
        public ulong NewsId { get; init; }
        public DateTime? PublishedAt { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Summary { get; init; }
        public int Score { get; init; }
    }

    internal sealed class PartnerRow
    {
        public ulong PartnerId { get; init; }
        public string? PublicSlug { get; init; }
        public string? Name { get; init; }
        public string? ShortName { get; init; }
        public string? Description { get; init; }
        public string? Country { get; init; }
        public int Score { get; init; }
    }

    internal sealed class FaqRow
    {
        public ulong FaqId { get; init; }
        public string FaqType { get; init; } = string.Empty;
        public int DisplayOrder { get; init; }
        public string? Question { get; init; }
        public string? Answer { get; init; }
        public int Score { get; init; }
    }

    internal sealed class GalleryRow
    {
        public ulong GalleryItemId { get; init; }
        public uint DisplayOrder { get; init; }
        public string MediaKind { get; init; } = string.Empty;
        public string CampusCode { get; init; } = string.Empty;
        public string CampusName { get; init; } = string.Empty;
        public string? City { get; init; }
        public ulong AreaId { get; init; }
        public ulong LocationId { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string? AreaName { get; init; }
        public string? LocationName { get; init; }
        public int Score { get; init; }
    }
}

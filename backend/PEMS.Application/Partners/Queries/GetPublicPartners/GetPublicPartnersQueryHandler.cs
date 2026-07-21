using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPublicPartners;

public sealed class GetPublicPartnersQueryHandler
    : IRequestHandler<GetPublicPartnersQuery, GetPublicPartnersResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IPartnerDescriptionTranslationCache _descriptionTranslator;

    public GetPublicPartnersQueryHandler(
        IApplicationDbContext db, IPartnerDescriptionTranslationCache descriptionTranslator)
    {
        _db = db;
        _descriptionTranslator = descriptionTranslator;
    }

    public async Task<GetPublicPartnersResponse> Handle(
        GetPublicPartnersQuery request, CancellationToken cancellationToken)
    {
        // Hard rule: the public surface never returns PENDING/REJECTED/PRIVATE/INTERNAL rows.
        var query = _db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{s}%")
                                     || (p.ShortName != null && EF.Functions.Like(p.ShortName, $"%{s}%")));
        }
        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            // `country` is free text (no FK/enum), so rows for the same country can carry different
            // raw spellings ("Việt Nam" / "VIETNAM" / " viet nam "). NormalizeKey can't be translated
            // to SQL, so resolve the filter value to every raw spelling sharing its normalized key
            // (mirrors GetPublicPartnerCountriesQueryHandler's grouping) and match any of them —
            // otherwise a filter value differing only by casing/diacritics/whitespace from what's
            // stored would silently return zero rows.
            var countryKey = PartnerNormalization.NormalizeKey(request.Country);
            var matchingRawCountries = await _db.Partners.AsNoTracking()
                .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                            && p.Visibility == PartnerVisibilities.Public
                            && p.Country != null && p.Country != "")
                .Select(p => p.Country!)
                .Distinct()
                .ToListAsync(cancellationToken);
            var rawMatches = matchingRawCountries
                .Where(c => PartnerNormalization.NormalizeKey(c) == countryKey)
                .ToList();
            query = query.Where(p => p.Country != null && rawMatches.Contains(p.Country));
        }

        if (!string.IsNullOrWhiteSpace(request.PartnerType) && PartnerTypes.All.Contains(request.PartnerType))
            query = query.Where(p => p.PartnerType == request.PartnerType);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);

        query = request.Sort?.Trim().ToLowerInvariant() switch
        {
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            "country" => query.OrderBy(p => p.Country).ThenBy(p => p.Name),
            _ => query.OrderBy(p => p.Name),
        };

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.PartnerId,
                p.Name,
                p.ShortName,
                p.Country,
                p.City,
                p.WebsiteUrl,
                p.Address,
                p.Description,
                p.PartnerType,
                p.LogoFileId,
                p.CoverFileId,
                p.PublicSlug,
                p.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        // Descriptions are Vietnamese-only in the DB by design (mirrors FAQ) — translated on
        // demand for a non-"vi" languageCode and cached 24h, same mechanism ViewFaqQueryHandler uses.
        var translated = await _descriptionTranslator.TranslateAsync(
            rows.Select(r => new PartnerDescriptionTranslationSource(r.PartnerId, r.Description, r.UpdatedAt)).ToList(),
            request.LanguageCode,
            cancellationToken);

        var items = rows.Select(p => new PublicPartnerDto
        {
            PartnerId = p.PartnerId,
            Name = p.Name,
            ShortName = p.ShortName,
            Country = p.Country,
            City = p.City,
            WebsiteUrl = p.WebsiteUrl,
            Address = p.Address,
            Description = translated.TryGetValue(p.PartnerId, out var d) ? d : p.Description,
            PartnerType = p.PartnerType,
            LogoFileId = p.LogoFileId,
            CoverFileId = p.CoverFileId,
            PublicSlug = p.PublicSlug,
        }).ToList();

        return new GetPublicPartnersResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}

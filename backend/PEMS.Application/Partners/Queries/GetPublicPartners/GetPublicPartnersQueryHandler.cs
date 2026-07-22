using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Application.Partners.Queries.GetPublicPartners;

/// <summary>
/// Public partner list — reads pre-translated VI/EN content straight from
/// <c>partner_translations</c>. Never calls a translation API: content is translated once, at
/// partner create/update time (see CreatePartnerCommandHandler/UpdatePartnerCommandHandler), and
/// simply falls back requested language → 'vi' → the legacy Vietnamese columns on
/// <c>partners</c> itself for any row that somehow predates the translation table being populated.
/// </summary>
public sealed class GetPublicPartnersQueryHandler
    : IRequestHandler<GetPublicPartnersQuery, GetPublicPartnersResponse>
{
    private readonly IApplicationDbContext _db;

    public GetPublicPartnersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<GetPublicPartnersResponse> Handle(
        GetPublicPartnersQuery request, CancellationToken cancellationToken)
    {
        var requestedLang = string.IsNullOrWhiteSpace(request.LanguageCode)
            ? NewsConstants.Languages.Default
            : request.LanguageCode.Trim().ToLowerInvariant();

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
            })
            .ToListAsync(cancellationToken);

        var partnerIds = rows.Select(r => r.PartnerId).ToList();
        var translations = partnerIds.Count == 0
            ? new List<PartnerTranslation>()
            : await _db.PartnerTranslations
                .AsNoTracking()
                .Where(t => partnerIds.Contains(t.PartnerId) && (t.LanguageCode == requestedLang || t.LanguageCode == "vi"))
                .ToListAsync(cancellationToken);

        var translationsByPartnerId = translations
            .GroupBy(t => t.PartnerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = rows.Select(p =>
        {
            var trs = translationsByPartnerId.GetValueOrDefault(p.PartnerId);
            var chosen = trs?.FirstOrDefault(t => t.LanguageCode == requestedLang)
                       ?? trs?.FirstOrDefault(t => t.LanguageCode == "vi");

            return new PublicPartnerDto
            {
                PartnerId = p.PartnerId,
                Name = chosen?.Name ?? p.Name,
                ShortName = chosen?.ShortName ?? p.ShortName,
                Country = chosen?.Country ?? p.Country,
                City = chosen?.City ?? p.City,
                WebsiteUrl = p.WebsiteUrl,
                Address = chosen?.Address ?? p.Address,
                Description = chosen?.Description ?? p.Description,
                PartnerType = p.PartnerType,
                LogoFileId = p.LogoFileId,
                CoverFileId = p.CoverFileId,
                PublicSlug = p.PublicSlug,
            };
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

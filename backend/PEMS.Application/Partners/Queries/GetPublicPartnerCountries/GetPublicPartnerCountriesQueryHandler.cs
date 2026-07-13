using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPublicPartnerCountries;

public sealed class GetPublicPartnerCountriesQueryHandler
    : IRequestHandler<GetPublicPartnerCountriesQuery, List<PublicPartnerCountryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetPublicPartnerCountriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<PublicPartnerCountryDto>> Handle(
        GetPublicPartnerCountriesQuery request, CancellationToken cancellationToken)
    {
        var rows = await _db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public
                        && p.Country != null && p.Country != "")
            .Select(p => p.Country!)
            .ToListAsync(cancellationToken);

        return rows
            // `country` is free text (no FK/enum — see Partner.Country), so the same country can be
            // stored with different casing/diacritics/whitespace ("Việt Nam", "viet nam", "VIETNAM ").
            // Group by the same accent/case-insensitive key used everywhere else partner text is
            // compared (PartnerNormalization.NormalizeKey), so those variants collapse into one row
            // instead of fragmenting the country count/list across the globe and /partners page.
            .GroupBy(c => PartnerNormalization.NormalizeKey(c))
            .Select(g =>
            {
                // Representative display value: the most frequent raw spelling in the group (ties
                // broken alphabetically) — this exact string is also what the /partners country
                // filter must match, so it round-trips as a valid `country` filter value.
                var representative = g
                    .GroupBy(raw => raw.Trim())
                    .OrderByDescending(rg => rg.Count())
                    .ThenBy(rg => rg.Key)
                    .First().Key;
                return new PublicPartnerCountryDto
                {
                    Value = representative,
                    Label = representative,
                    Count = g.Count(),
                };
            })
            .OrderBy(c => c.Value)
            .ToList();
    }
}

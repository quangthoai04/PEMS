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
        var groups = await _db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public
                        && p.Country != null && p.Country != "")
            .GroupBy(p => p.Country!)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return groups
            // Trim + re-group here (in-memory) so "Việt Nam" and " Việt Nam " don't become two rows —
            // EF/MySQL collation already trims most whitespace, but this keeps the guarantee explicit.
            .GroupBy(g => g.Country.Trim())
            .Select(g => new PublicPartnerCountryDto
            {
                Value = g.Key,
                Label = g.Key,
                Count = g.Sum(x => x.Count),
            })
            .OrderBy(c => c.Value)
            .ToList();
    }
}

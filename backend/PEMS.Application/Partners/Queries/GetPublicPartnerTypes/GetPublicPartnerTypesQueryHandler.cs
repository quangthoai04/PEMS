using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPublicPartnerTypes;

public sealed class GetPublicPartnerTypesQueryHandler
    : IRequestHandler<GetPublicPartnerTypesQuery, List<PublicPartnerTypeDto>>
{
    private readonly IApplicationDbContext _db;

    public GetPublicPartnerTypesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<PublicPartnerTypeDto>> Handle(
        GetPublicPartnerTypesQuery request, CancellationToken cancellationToken)
    {
        var groups = await _db.Partners.AsNoTracking()
            .Where(p => p.ProfileStatus == PartnerProfileStatuses.Approved
                        && p.Visibility == PartnerVisibilities.Public)
            .GroupBy(p => p.PartnerType)
            .Select(g => new PublicPartnerTypeDto
            {
                Value = g.Key,
                Label = g.Key,
                Count = g.Count(),
            })
            .OrderBy(t => t.Value)
            .ToListAsync(cancellationToken);

        return groups;
    }
}

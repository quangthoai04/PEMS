using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Campuses.Queries.GetActiveCampuses;

public sealed class GetActiveCampusesQueryHandler : IRequestHandler<GetActiveCampusesQuery, List<ActiveCampusDto>>
{
    private readonly IApplicationDbContext _db;

    public GetActiveCampusesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ActiveCampusDto>> Handle(GetActiveCampusesQuery request, CancellationToken cancellationToken)
    {
        return await _db.Campuses
            .AsNoTracking()
            .Where(c => c.Status == "ACTIVE")
            .OrderBy(c => c.CampusCode)
            .Select(c => new ActiveCampusDto
            {
                CampusId = c.CampusId,
                CampusCode = c.CampusCode,
                CampusName = c.Name
            })
            .ToListAsync(cancellationToken);
    }
}

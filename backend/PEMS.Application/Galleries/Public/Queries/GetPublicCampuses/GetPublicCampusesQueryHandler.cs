using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicCampuses;

/// <summary>
/// Returns the ACTIVE campuses (id, code, name, city) ordered by campus id. There is no per-campus
/// cover image column in the schema yet, so <c>CoverUrl</c> is left null and the frontend falls back to
/// a static banner (UC §7.1 note). Read-only / anonymous — exposes no admin or audit fields.
/// </summary>
public sealed class GetPublicCampusesQueryHandler : IRequestHandler<GetPublicCampusesQuery, PublicCampusListDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublicCampusesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PublicCampusListDto> Handle(GetPublicCampusesQuery request, CancellationToken cancellationToken)
    {
        var items = await _db.Campuses.AsNoTracking()
            .Where(c => c.Status == "ACTIVE")
            .OrderBy(c => c.CampusId)
            .Select(c => new PublicCampusDto
            {
                CampusId = c.CampusId,
                CampusCode = c.CampusCode,
                CampusName = c.Name,
                City = c.City,
            })
            .ToListAsync(cancellationToken);

        return new PublicCampusListDto { Items = items };
    }
}

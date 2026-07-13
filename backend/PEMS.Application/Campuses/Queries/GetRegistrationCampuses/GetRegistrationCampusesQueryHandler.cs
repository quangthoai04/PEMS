using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Campuses.Queries.GetRegistrationCampuses;

public sealed class GetRegistrationCampusesQueryHandler
    : IRequestHandler<GetRegistrationCampusesQuery, List<RegistrationCampusDto>>
{
    private readonly IApplicationDbContext _db;

    public GetRegistrationCampusesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<RegistrationCampusDto>> Handle(
        GetRegistrationCampusesQuery request, CancellationToken cancellationToken)
    {
        // ACTIVE is a precondition of availability — pre-filter, then run the shared evaluator
        // so this endpoint can never drift from the submit-side recheck (§9).
        var activeCampusIds = await _db.Campuses.AsNoTracking()
            .Where(c => c.Status == EntityStatuses.Active)
            .Select(c => c.CampusId)
            .ToListAsync(cancellationToken);

        var snapshots = await CampusAvailabilityEvaluator.EvaluateAsync(
            _db, activeCampusIds, cancellationToken);

        return snapshots.Values
            .Where(s => s.IsAvailableForVisitRegistration)
            .OrderBy(s => s.CampusCode)
            .Select(s => new RegistrationCampusDto
            {
                CampusId = s.CampusId,
                CampusCode = s.CampusCode,
                CampusName = s.Name,
                City = s.City,
            })
            .ToList();
    }
}

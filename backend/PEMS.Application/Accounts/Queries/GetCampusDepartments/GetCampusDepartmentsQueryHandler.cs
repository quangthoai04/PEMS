using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Queries.GetCampusDepartments;

public sealed class GetCampusDepartmentsQueryHandler
    : IRequestHandler<GetCampusDepartmentsQuery, IReadOnlyList<CampusDepartmentDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCampusDepartmentsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CampusDepartmentDto>> Handle(
        GetCampusDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var campusId = _currentUser.PrimaryCampusId;
        if (campusId is null)
            return new List<CampusDepartmentDto>();

        return await _db.Departments.AsNoTracking()
            .Where(d => d.CampusId == campusId
                        && d.DepartmentType == "GENERAL"
                        && d.Status == EntityStatuses.Active)
            .OrderBy(d => d.Name)
            .Select(d => new CampusDepartmentDto
            {
                DepartmentId = d.DepartmentId,
                Name = d.Name,
                HasHead = d.HeadUserId != null,
            })
            .ToListAsync(cancellationToken);
    }
}

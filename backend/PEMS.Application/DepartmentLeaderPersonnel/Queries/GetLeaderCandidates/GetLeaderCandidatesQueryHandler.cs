using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.GetLeaderCandidates;

/// <summary>
/// Spec §16. A candidate is an ACTIVE <c>DEPARTMENT + STAFF</c> member of the caller's own department
/// sitting in that department's campus. The caller and the seated head are excluded — the transfer
/// command re-applies every one of these predicates under a row lock before it writes.
/// </summary>
public sealed class GetLeaderCandidatesQueryHandler
    : IRequestHandler<GetLeaderCandidatesQuery, GetLeaderCandidatesResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;

    public GetLeaderCandidatesQueryHandler(
        IApplicationDbContext db, IDepartmentLeaderPersonnelScopeService scopeService)
    {
        _db = db;
        _scopeService = scopeService;
    }

    public async Task<GetLeaderCandidatesResponse> Handle(
        GetLeaderCandidatesQuery request, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        var department = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == scope.DepartmentId)
            .Select(d => new
            {
                d.HeadUserId,
                HeadName = d.HeadUser != null ? d.HeadUser.FullName : null,
            })
            .FirstAsync(cancellationToken);

        var candidates = await _db.Users.AsNoTracking()
            .Where(u => u.DepartmentId == scope.DepartmentId
                        && u.Role.RoleCode == RoleCodes.Department
                        && u.SubRole == UserSubRoles.Staff
                        && u.Status == UserStatuses.Active
                        && u.PrimaryCampusId == scope.CampusId
                        && u.UserId != scope.ActorUserId
                        && (department.HeadUserId == null || u.UserId != department.HeadUserId))
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.UserId)
            .Select(u => new LeaderCandidateDto
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                AvatarUrl = u.AvatarUrl,
            })
            .ToListAsync(cancellationToken);

        return new GetLeaderCandidatesResponse
        {
            Items = candidates,
            CurrentLeaderUserId = department.HeadUserId,
            CurrentLeaderName = department.HeadName,
        };
    }
}

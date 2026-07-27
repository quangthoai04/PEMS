using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.GetMyDepartment;

/// <summary>
/// Spec §8. Resolves the caller's department through the shared scope gate and projects its header +
/// status breakdown. Read-only: <c>AsNoTracking()</c> throughout.
/// </summary>
public sealed class GetMyDepartmentQueryHandler : IRequestHandler<GetMyDepartmentQuery, GetMyDepartmentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;

    public GetMyDepartmentQueryHandler(IApplicationDbContext db, IDepartmentLeaderPersonnelScopeService scopeService)
    {
        _db = db;
        _scopeService = scopeService;
    }

    public async Task<GetMyDepartmentResponse> Handle(
        GetMyDepartmentQuery request, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        var department = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == scope.DepartmentId)
            .Select(d => new
            {
                d.DepartmentId,
                d.Name,
                d.DepartmentType,
                d.Status,
                d.CampusId,
                CampusName = d.Campus.Name,
                d.HeadUserId,
                HeadName = d.HeadUser != null ? d.HeadUser.FullName : null,
            })
            .FirstAsync(cancellationToken);

        // One pass over the department's members; counted in the database, never in memory.
        var counts = await _db.Users.AsNoTracking()
            .Where(u => u.DepartmentId == scope.DepartmentId && u.Role.RoleCode == RoleCodes.Department)
            .GroupBy(u => u.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountOf(string status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        return new GetMyDepartmentResponse
        {
            DepartmentId = department.DepartmentId,
            DepartmentName = department.Name,
            DepartmentType = department.DepartmentType,
            DepartmentStatus = department.Status,
            CampusId = department.CampusId,
            CampusName = department.CampusName,
            CurrentLeaderUserId = department.HeadUserId,
            CurrentLeaderName = department.HeadName,
            TotalPersonnelCount = counts.Sum(c => c.Count),
            ActivePersonnelCount = CountOf(UserStatuses.Active),
            InactivePersonnelCount = CountOf(UserStatuses.Inactive),
            PendingEmailConfirmationCount = CountOf(UserStatuses.PendingEmailConfirmation),
            LockedPersonnelCount = CountOf(UserStatuses.Locked),
        };
    }
}

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.GetPersonnelStatusImpact;

/// <summary>
/// Spec §14. Delegates the whole verdict to <see cref="DepartmentPersonnelStatusRules"/> — the same
/// code the write command runs — so the modal can never promise something the toggle then refuses.
/// Strictly read-only, including the responsibility scan.
/// </summary>
public sealed class GetPersonnelStatusImpactQueryHandler
    : IRequestHandler<GetPersonnelStatusImpactQuery, GetPersonnelStatusImpactResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;
    private readonly IDateTimeService _clock;

    public GetPersonnelStatusImpactQueryHandler(
        IApplicationDbContext db,
        IDepartmentLeaderPersonnelScopeService scopeService,
        IDateTimeService clock)
    {
        _db = db;
        _scopeService = scopeService;
        _clock = clock;
    }

    public async Task<GetPersonnelStatusImpactResponse> Handle(
        GetPersonnelStatusImpactQuery request, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);
        var target = await _scopeService.GetScopedPersonnelAsync(scope, request.UserId, cancellationToken);

        var department = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == scope.DepartmentId)
            .Select(d => new { d.Status, d.HeadUserId, CampusStatus = d.Campus.Status })
            .FirstAsync(cancellationToken);

        var targetStatus = request.TargetStatus.Trim().ToUpperInvariant();

        var impact = await DepartmentPersonnelStatusRules.EvaluateAsync(
            _db, scope, target, targetStatus,
            department.Status, department.CampusStatus, department.HeadUserId,
            _clock.VietnamNow, cancellationToken);

        return new GetPersonnelStatusImpactResponse
        {
            UserId = impact.UserId,
            CurrentStatus = impact.CurrentStatus,
            TargetStatus = impact.TargetStatus,
            CanChangeStatus = impact.CanChangeStatus,
            ActiveSessionCount = impact.ActiveSessionCount,
            Blockers = impact.Blockers
                .Select(b => new StatusImpactBlockerDto { Code = b.Code, Count = b.Count, Message = b.Message })
                .ToList(),
            Warnings = impact.Warnings
                .Select(w => new StatusImpactWarningDto { Code = w.Code, Count = w.Count, Message = w.Message })
                .ToList(),
        };
    }
}

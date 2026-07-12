using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Departments.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Departments.Queries.GetDepartmentStatusImpact;

/// <summary>
/// UC-106 impact preview. Same actor/scope/type guards as ManageDepartmentStatus; computes
/// the shared <see cref="DepartmentStatusImpactCalculator"/> result without changing anything.
/// </summary>
public sealed class GetDepartmentStatusImpactQueryHandler
    : IRequestHandler<GetDepartmentStatusImpactQuery, GetDepartmentStatusImpactResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetDepartmentStatusImpactQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<GetDepartmentStatusImpactResponse> Handle(
        GetDepartmentStatusImpactQuery request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderDepartmentScope.EnsureStaffLeaderCampus(_currentUser);
        var targetStatus = request.NewStatus.Trim().ToUpperInvariant();

        var department = await _db.Departments
            .Include(d => d.Campus)
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken)
            ?? throw new NotFoundException("Department", request.DepartmentId);

        if (department.CampusId != campusId)
            throw new NotFoundException("Department", request.DepartmentId);

        if (department.DepartmentType != "GENERAL")
            throw new AuthBusinessException(
                DepartmentErrorCodes.DepartmentTypeNotManageable,
                "Không thể thay đổi trạng thái của phòng ban mặc định (IC).", 403);

        // Enable preview: no accounts/sessions are affected; only the campus gate applies.
        if (targetStatus == EntityStatuses.Active)
        {
            var campusActive = department.Campus is not null
                               && department.Campus.Status == EntityStatuses.Active;
            return new GetDepartmentStatusImpactResponse
            {
                DepartmentId = department.DepartmentId,
                Name = department.Name,
                CurrentStatus = department.Status,
                TargetStatus = targetStatus,
                CanChangeStatus = campusActive && department.Status != EntityStatuses.Active,
                Blockers = campusActive
                    ? new List<DepartmentStatusBlocker>()
                    : new List<DepartmentStatusBlocker>
                    {
                        new DepartmentStatusBlocker
                        {
                            Type = "CAMPUS_INACTIVE",
                            Count = 1,
                            Message = "Không thể kích hoạt phòng ban trong cơ sở đã ngừng hoạt động.",
                        },
                    },
            };
        }

        var impact = await DepartmentStatusImpactCalculator.ComputeDisableImpactAsync(
            _db, department.DepartmentId, _clock.VietnamNow, cancellationToken);

        return new GetDepartmentStatusImpactResponse
        {
            DepartmentId = department.DepartmentId,
            Name = department.Name,
            CurrentStatus = department.Status,
            TargetStatus = targetStatus,
            AffectedAccountCount = impact.AffectedAccountCount,
            ActiveSessionCount = impact.ActiveSessionCount,
            CanChangeStatus = !impact.HasBlockers && department.Status == EntityStatuses.Active,
            Blockers = impact.Blockers,
        };
    }
}

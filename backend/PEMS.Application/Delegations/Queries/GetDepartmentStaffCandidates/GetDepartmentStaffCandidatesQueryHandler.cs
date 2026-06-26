using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.GetParticipantCandidates;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Queries.GetDepartmentStaffCandidates;

public sealed class GetDepartmentStaffCandidatesQueryHandler
    : IRequestHandler<GetDepartmentStaffCandidatesQuery, IReadOnlyList<ParticipantCandidateDto>>
{
    private const int MaxResults = 50;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDepartmentStaffCandidatesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ParticipantCandidateDto>> Handle(
        GetDepartmentStaffCandidatesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        // Only a Department Leader may list their own department's staff for assignment.
        if (_currentUser.RoleCode != RoleCodes.Department
            || !string.Equals(_currentUser.SubRole, UserSubRoles.Leader, System.StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Chỉ Trưởng phòng mới được xem danh sách nhân sự của phòng.");

        if (_currentUser.DepartmentId is null || _currentUser.PrimaryCampusId is null)
            throw new ForbiddenException("Tài khoản chưa được gán phòng ban / cơ sở.");

        var departmentId = _currentUser.DepartmentId.Value;
        var campusId = _currentUser.PrimaryCampusId.Value;

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();

        // Staff already occupying a slot in this instance are excluded.
        var blockingUserIds = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                        && (p.Status == ParticipantStatuses.Invited
                            || p.Status == ParticipantStatuses.Accepted
                            || p.Status == ParticipantStatuses.Assigned))
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        var query =
            from u in _db.Users
            join r in _db.Roles on u.RoleId equals r.RoleId
            where r.RoleCode == RoleCodes.Department
                  && u.SubRole == UserSubRoles.Staff
                  && u.Status == UserStatuses.Active
                  && u.DepartmentId == departmentId
                  && u.PrimaryCampusId == campusId
                  && !blockingUserIds.Contains(u.UserId)
            select u;

        if (keyword != null)
            query = query.Where(u => u.FullName.Contains(keyword) || u.Email.Contains(keyword));

        var rows = await query
            .OrderBy(u => u.FullName)
            .Take(MaxResults)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                u.SubRole,
                u.DepartmentId,
                u.PrimaryCampusId,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return new List<ParticipantCandidateDto>();

        var departmentName = await _db.Departments
            .Where(d => d.DepartmentId == departmentId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == campusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        // Conflict detection is not required for department staff (spec §13.5) — kept at 0 to keep
        // the assignment screen fast. Add ScheduleConflictResolver here if it becomes a requirement.
        return rows.Select(r => new ParticipantCandidateDto
        {
            UserId = r.UserId,
            FullName = r.FullName,
            Email = r.Email,
            Phone = r.Phone,
            RoleCode = RoleCodes.Department,
            SubRole = r.SubRole,
            DepartmentId = r.DepartmentId,
            DepartmentName = departmentName,
            CampusId = r.PrimaryCampusId,
            CampusName = campusName,
            ConflictCount = 0,
            HasPrivateConflict = false,
            ConflictSummary = null,
            CanInvite = true,
            DisabledReason = null,
        }).ToList();
    }
}

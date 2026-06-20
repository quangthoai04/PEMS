using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetHostCandidates;

public sealed class GetHostCandidatesQueryHandler
    : IRequestHandler<GetHostCandidatesQuery, IReadOnlyList<HostCandidateDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetHostCandidatesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<HostCandidateDto>> Handle(
        GetHostCandidatesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == SubRoles.Leader))
            throw new ForbiddenException("Chỉ Staff Leader mới được xem danh sách host.");

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (_currentUser.PrimaryCampusId != instance.CampusId)
            throw new ForbiddenException("Cơ sở này không thuộc phạm vi phụ trách của bạn.");

        var campusId = instance.CampusId;
        var windowStart = instance.PlannedStartAt;
        var windowEnd = instance.PlannedEndAt;

        // Active STAFF of the campus (Leader + Staff sub-roles are all eligible hosts).
        var candidates = await (
            from u in _db.Users
            join r in _db.Roles on u.RoleId equals r.RoleId
            join d in _db.Departments on u.DepartmentId equals d.DepartmentId into depts
            from dep in depts.DefaultIfEmpty()
            where r.RoleCode == RoleCodes.Staff
                  && u.SubRole == SubRoles.Staff
                  && u.PrimaryCampusId == campusId
                  && u.Status == UserStatuses.Active
                  && u.UserId != _currentUser.UserId
            select new HostCandidateDto
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                CampusId = u.PrimaryCampusId,
                DepartmentName = dep != null ? dep.Name : null,
                SubRole = u.SubRole,
            }).ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return candidates;

        var candidateIds = candidates.Select(c => c.UserId).ToList();

        // Every active hosting assignment for these candidates (excluding this instance).
        var busy = await _db.VisitRequestCampuses
            .Where(c => c.CurrentHostUserId != null
                        && candidateIds.Contains(c.CurrentHostUserId.Value)
                        && c.VisitInstanceId != instance.VisitInstanceId
                        && c.Status != VisitInstanceStatus.Cancelled
                        && c.Status != VisitInstanceStatus.Closed)
            .Select(c => new
            {
                c.VisitInstanceId,
                c.VisitRequestId,
                HostUserId = c.CurrentHostUserId!.Value,
                c.PlannedStartAt,
                c.PlannedEndAt,
                DelegationName = c.VisitRequest.DelegationName
            })
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var mine = busy.Where(b => b.HostUserId == candidate.UserId).ToList();
            candidate.ActiveAssignmentCount = mine.Count;

            // Overlap: existing.start < new.end AND existing.end > new.start.
            candidate.Conflicts = mine
                .Where(b => b.PlannedStartAt < windowEnd && b.PlannedEndAt > windowStart)
                .Select(b => new HostConflictDto
                {
                    VisitRequestId = b.VisitRequestId,
                    VisitInstanceId = b.VisitInstanceId,
                    DelegationName = b.DelegationName,
                    StartTime = b.PlannedStartAt,
                    EndTime = b.PlannedEndAt
                })
                .ToList();
            candidate.HasScheduleConflict = candidate.Conflicts.Count > 0;
        }

        return candidates
            .OrderBy(c => c.HasScheduleConflict)        // conflict-free first
            .ThenBy(c => c.ActiveAssignmentCount)        // least loaded first
            .ThenBy(c => c.FullName)
            .ToList();
    }
}

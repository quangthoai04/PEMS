using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.GetHostCandidates;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetCreateHostCandidates;

/// <summary>
/// Same eligibility rules as <see cref="GetHostCandidates.GetHostCandidatesQueryHandler"/>
/// (ACTIVE same-campus IC Staff + the Leader themself), but keyed off the caller's own
/// campus because the campus instance does not exist yet at create time.
/// </summary>
public sealed class GetCreateHostCandidatesQueryHandler
    : IRequestHandler<GetCreateHostCandidatesQuery, IReadOnlyList<HostCandidateDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCreateHostCandidatesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<HostCandidateDto>> Handle(
        GetCreateHostCandidatesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader))
            throw new ForbiddenException("Chỉ Staff Leader mới được xem danh sách host.");

        var campusId = _currentUser.PrimaryCampusId
            ?? throw new ForbiddenException("Tài khoản Staff Leader chưa được gán cơ sở.");

        var candidates = await (
            from u in _db.Users
            join r in _db.Roles on u.RoleId equals r.RoleId
            join d in _db.Departments on u.DepartmentId equals d.DepartmentId
            where r.RoleCode == RoleCodes.Staff
                  && u.SubRole == UserSubRoles.Staff
                  && u.PrimaryCampusId == campusId
                  && u.Status == UserStatuses.Active
                  && d.DepartmentType == "IC"
                  && d.Status == EntityStatuses.Active
                  && u.UserId != _currentUser.UserId
            select new HostCandidateDto
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                CampusId = u.PrimaryCampusId,
                DepartmentName = d.Name,
                SubRole = u.SubRole,
                RoleLabel = "IC Staff",
            }).ToListAsync(cancellationToken);

        var self = await (
            from u in _db.Users
            join d in _db.Departments on u.DepartmentId equals d.DepartmentId into dj
            from d in dj.DefaultIfEmpty()
            where u.UserId == _currentUser.UserId
                  && u.PrimaryCampusId == campusId
                  && u.Status == UserStatuses.Active
            select new HostCandidateDto
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                CampusId = u.PrimaryCampusId,
                DepartmentName = d != null ? d.Name : null,
                SubRole = u.SubRole,
                RoleLabel = "Staff Leader",
                IsSelf = true,
                IsStaffLeaderSelfHostOption = true,
            }).FirstOrDefaultAsync(cancellationToken);

        if (self is not null)
            candidates.Insert(0, self);

        if (candidates.Count == 0
            || request.WindowStartAt is not { } windowStart
            || request.WindowEndAt is not { } windowEnd
            || windowEnd <= windowStart)
        {
            return candidates;
        }

        var candidateIds = candidates.Select(c => c.UserId).ToList();

        // Non-blocking conflict warnings — same overlap rule as the per-instance API.
        var busy = await _db.VisitRequestCampuses
            .Where(c => c.CurrentHostUserId != null
                        && candidateIds.Contains(c.CurrentHostUserId.Value)
                        && (c.Status == VisitInstanceStatus.Assigned
                            || c.Status == VisitInstanceStatus.BeforeVisit
                            || c.Status == VisitInstanceStatus.DuringVisit)
                        && c.PlannedStartAt < windowEnd
                        && c.PlannedEndAt > windowStart)
            .Select(c => new
            {
                HostUserId = c.CurrentHostUserId!.Value,
                c.VisitInstanceId,
                c.PlannedStartAt,
                c.PlannedEndAt,
                // Conflict label: mixed v2 shows the BUSY instance's own detail name.
                DelegationName = c.FormDetail != null ? c.FormDetail.DelegationName : null,
            })
            .ToListAsync(cancellationToken);

        var calendarConflicts = await _db.CalendarEvents
            .Where(e => candidateIds.Contains(e.OwnerUserId)
                        && e.Status == "ACTIVE"
                        && e.DeletedAt == null
                        && e.StartAt < windowEnd
                        && e.EndAt > windowStart)
            .Select(e => new { e.OwnerUserId, e.CalendarEventId, e.Title, e.SourceType, e.Visibility, e.StartAt, e.EndAt })
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var instanceConflicts = busy
                .Where(b => b.HostUserId == candidate.UserId)
                .Select(b => new HostConflictDto
                {
                    Source = "VISIT_INSTANCE",
                    Title = b.DelegationName ?? "Đoàn khách khác",
                    StartAt = b.PlannedStartAt,
                    EndAt = b.PlannedEndAt,
                    VisitInstanceId = b.VisitInstanceId,
                });

            var calConflicts = calendarConflicts
                .Where(e => e.OwnerUserId == candidate.UserId)
                .Select(e => new HostConflictDto
                {
                    Source = "CALENDAR",
                    Title = (e.SourceType == "PERSONAL" && e.Visibility == "PRIVATE") ? "Lịch cá nhân" : e.Title,
                    StartAt = e.StartAt,
                    EndAt = e.EndAt,
                    CalendarEventId = e.CalendarEventId,
                });

            candidate.Conflicts = instanceConflicts.Concat(calConflicts).OrderBy(c => c.StartAt).ToList();
            candidate.ConflictCount = candidate.Conflicts.Count;
            candidate.HasScheduleConflict = candidate.ConflictCount > 0;
        }

        return candidates
            .OrderByDescending(c => c.IsStaffLeaderSelfHostOption)
            .ThenBy(c => c.HasScheduleConflict)
            .ThenBy(c => c.ConflictCount)
            .ThenBy(c => c.FullName)
            .ToList();
    }
}

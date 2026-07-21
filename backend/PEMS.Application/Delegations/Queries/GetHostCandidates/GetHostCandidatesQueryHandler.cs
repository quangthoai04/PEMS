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

        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader))
            throw new ForbiddenException("Chỉ Staff Leader mới được xem danh sách host.");

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (_currentUser.PrimaryCampusId != instance.CampusId)
            throw new ForbiddenException("Cơ sở này không thuộc phạm vi phụ trách của bạn.");

        var campusId = instance.CampusId;
        // Target window comes from the current campus instance (planned_start_at / planned_end_at).
        var windowStart = instance.PlannedStartAt;
        var windowEnd = instance.PlannedEndAt;

        // Eligible hosts (campus-independent approval):
        //   1. active IC-department STAFF of this campus, and
        //   2. the calling Staff Leader THEMSELF ("Tôi làm host chính") — never another Staff Leader.
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

        // Self-host option: the approving Staff Leader (already validated STAFF+LEADER above).
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

        if (candidates.Count == 0)
            return candidates;

        var candidateIds = candidates.Select(c => c.UserId).ToList();

        // ── Conflict source A: personal/other calendar events overlapping the window ──
        // Overlap rule: existing.start < target.end AND existing.end > target.start
        // (an event that ends exactly when the visit starts — or starts when it ends — is NOT a conflict).
        var calendarConflicts = await _db.CalendarEvents
            .Where(e => candidateIds.Contains(e.OwnerUserId)
                        && e.Status == "ACTIVE"
                        && e.DeletedAt == null
                        && e.StartAt < windowEnd
                        && e.EndAt > windowStart)
            .Select(e => new
            {
                e.OwnerUserId,
                e.CalendarEventId,
                e.Title,
                e.SourceType,
                e.Visibility,
                e.StartAt,
                e.EndAt,
            })
            .ToListAsync(cancellationToken);

        // ── Conflict source B: other live hosting assignments overlapping the window ──
        var busy = await _db.VisitRequestCampuses
            .Where(c => c.CurrentHostUserId != null
                        && candidateIds.Contains(c.CurrentHostUserId.Value)
                        && c.VisitInstanceId != instance.VisitInstanceId
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
                DelegationName = c.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                    ? (c.FormDetail != null ? c.FormDetail.DelegationName : null)
                    : c.VisitRequest.DelegationName,
            })
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
                    // Never leak the content of a private personal event.
                    Title = (e.SourceType == "PERSONAL" && e.Visibility == "PRIVATE") ? "Lịch cá nhân" : e.Title,
                    StartAt = e.StartAt,
                    EndAt = e.EndAt,
                    CalendarEventId = e.CalendarEventId,
                });

            candidate.Conflicts = instanceConflicts
                .Concat(calConflicts)
                .OrderBy(c => c.StartAt)
                .ToList();
            candidate.ConflictCount = candidate.Conflicts.Count;
            candidate.HasScheduleConflict = candidate.ConflictCount > 0;
        }

        return candidates
            .OrderByDescending(c => c.IsStaffLeaderSelfHostOption) // "Tôi làm host chính" on top
            .ThenBy(c => c.HasScheduleConflict)    // conflict-free first
            .ThenBy(c => c.ConflictCount)          // fewer conflicts first
            .ThenBy(c => c.FullName)
            .ToList();
    }
}

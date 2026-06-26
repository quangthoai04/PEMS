using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Queries.GetParticipantCandidates;

public sealed class GetParticipantCandidatesQueryHandler
    : IRequestHandler<GetParticipantCandidatesQuery, IReadOnlyList<ParticipantCandidateDto>>
{
    private const int MaxResults = 25;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetParticipantCandidatesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ParticipantCandidateDto>> Handle(
        GetParticipantCandidatesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var type = (request.Type ?? string.Empty).Trim().ToUpperInvariant();
        if (type != ParticipantCandidateTypes.IcSupport && type != ParticipantCandidateTypes.Student)
            throw new ValidationException("Loại thành phần tham gia không hợp lệ.");

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Only the official host of this instance picks supporters (the FE shows these dropdowns to
        // the host alone; we re-check on the server so the endpoint can't be abused).
        if (instance.CurrentHostUserId != _currentUser.UserId.Value)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được mời thành phần tham gia.");

        var campusId = instance.CampusId;
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();

        // Users who already occupy a slot in THIS instance (any status) — active ones are excluded
        // from the candidate list; DECLINED/REMOVED rows are allowed back so the host can re-invite.
        var blockingUserIds = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                        && (p.Status == ParticipantStatuses.Invited
                            || p.Status == ParticipantStatuses.Accepted
                            || p.Status == ParticipantStatuses.Assigned))
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        // Base query (entity-rooted; names enriched after materialization).
        var query =
            from u in _db.Users
            join r in _db.Roles on u.RoleId equals r.RoleId
            where u.Status == UserStatuses.Active
                  && u.PrimaryCampusId == campusId
                  && !blockingUserIds.Contains(u.UserId)
                  && u.UserId != instance.CurrentHostUserId
            select new { u, RoleCode = r.RoleCode };

        if (type == ParticipantCandidateTypes.IcSupport)
        {
            // Staff (sub_role STAFF) of an ACTIVE IC department on this campus — never the Staff Leader.
            query =
                from x in query
                join d in _db.Departments on x.u.DepartmentId equals d.DepartmentId
                where x.RoleCode == RoleCodes.Staff
                      && x.u.SubRole == UserSubRoles.Staff
                      && d.DepartmentType == "IC"
                      && d.Status == EntityStatuses.Active
                select x;
        }
        else // STUDENT
        {
            query = query.Where(x => x.RoleCode == RoleCodes.Student);
        }

        if (keyword != null)
        {
            query = type == ParticipantCandidateTypes.Student
                ? query.Where(x => x.u.FullName.Contains(keyword)
                                   || x.u.Email.Contains(keyword)
                                   || (x.u.StudentCode != null && x.u.StudentCode.Contains(keyword)))
                : query.Where(x => x.u.FullName.Contains(keyword) || x.u.Email.Contains(keyword));
        }

        var rows = await query
            .OrderBy(x => x.u.FullName)
            .Take(MaxResults)
            .Select(x => new
            {
                x.u.UserId,
                x.u.FullName,
                x.u.Email,
                x.u.Phone,
                x.u.StudentCode,
                RoleCode = x.RoleCode,
                x.u.SubRole,
                x.u.DepartmentId,
                x.u.PrimaryCampusId,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return new List<ParticipantCandidateDto>();

        // ── Enrich: department names + campus name + schedule conflicts ──
        var deptIds = rows.Where(r => r.DepartmentId.HasValue).Select(r => r.DepartmentId!.Value).Distinct().ToList();
        var deptNames = deptIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Departments
                .Where(d => deptIds.Contains(d.DepartmentId))
                .ToDictionaryAsync(d => d.DepartmentId, d => d.Name, cancellationToken);

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == campusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var conflicts = await ScheduleConflictResolver.ResolveAsync(
            _db, rows.Select(r => r.UserId).ToList(),
            instance.PlannedStartAt, instance.PlannedEndAt, instance.VisitInstanceId, cancellationToken);

        return rows.Select(r =>
        {
            var c = conflicts[r.UserId];
            return new ParticipantCandidateDto
            {
                UserId = r.UserId,
                FullName = r.FullName,
                Email = r.Email,
                Phone = r.Phone,
                StudentCode = r.StudentCode,
                RoleCode = r.RoleCode,
                SubRole = r.SubRole,
                DepartmentId = r.DepartmentId,
                DepartmentName = r.DepartmentId.HasValue && deptNames.TryGetValue(r.DepartmentId.Value, out var dn) ? dn : null,
                CampusId = r.PrimaryCampusId,
                CampusName = campusName,
                ConflictCount = c.Count,
                HasPrivateConflict = c.Any(x => x.IsPrivate),
                ConflictSummary = ScheduleConflictResolver.BuildSummary(c),
                CanInvite = true,
                DisabledReason = null,
            };
        }).ToList();
    }
}

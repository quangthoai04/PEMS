using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Queries.GetSupportDepartments;

public sealed class GetSupportDepartmentsQueryHandler
    : IRequestHandler<GetSupportDepartmentsQuery, IReadOnlyList<SupportDepartmentDto>>
{
    private const int MaxResults = 50;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSupportDepartmentsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SupportDepartmentDto>> Handle(
        GetSupportDepartmentsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Host-only: this list feeds BOTH the participant-invitation dropdown and the logistics
        // department picker on the host's prep screen.
        if (instance.CurrentHostUserId != _currentUser.UserId.Value)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được xem danh sách phòng ban hỗ trợ.");

        var campusId = instance.CampusId;
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();

        var deptQuery = _db.Departments
            .Where(d => d.CampusId == campusId
                        && d.DepartmentType == "GENERAL"
                        && d.Status == EntityStatuses.Active);
        if (keyword != null)
            deptQuery = deptQuery.Where(d => d.Name.Contains(keyword));

        var departments = await deptQuery
            .OrderBy(d => d.Name)
            .Take(MaxResults)
            .Select(d => new { d.DepartmentId, d.Name, d.HeadUserId })
            .ToListAsync(cancellationToken);

        if (departments.Count == 0)
            return new List<SupportDepartmentDto>();

        var deptIds = departments.Select(d => d.DepartmentId).ToList();

        // Active DEPARTMENT Leaders of these departments on this campus (the real invitees).
        var leaders = await (
            from u in _db.Users
            join r in _db.Roles on u.RoleId equals r.RoleId
            where r.RoleCode == RoleCodes.Department
                  && u.SubRole == UserSubRoles.Leader
                  && u.Status == UserStatuses.Active
                  && u.PrimaryCampusId == campusId
                  && u.DepartmentId != null
                  && deptIds.Contains(u.DepartmentId.Value)
            select new { u.UserId, u.FullName, u.Email, DepartmentId = u.DepartmentId!.Value })
            .ToListAsync(cancellationToken);

        // Name, then id. The id is what makes the order TOTAL: two active leaders sharing a full name
        // is rare but permitted, and sorting by name alone leaves their relative order unspecified — so
        // this list and the invite handler could name different people for the same department. They
        // must agree, because the email approval is scoped to whoever this list showed the host.
        var leadersByDept = leaders
            .GroupBy(l => l.DepartmentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.FullName).ThenBy(x => x.UserId).ToList());

        // Leaders already occupying an active slot in this instance → cannot be re-invited as a
        // participant. This ONLY affects CanInviteParticipant — logistics stays available because
        // inviting the leader to join the visit and sending their department a logistics request
        // are independent businesses (DECLINED/REMOVED rows block neither).
        var leaderIds = leaders.Select(l => l.UserId).Distinct().ToList();
        var alreadyInvitedLeaderIds = leaderIds.Count == 0
            ? new List<ulong>()
            : await _db.VisitParticipants
                .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                            && leaderIds.Contains(p.UserId)
                            && (p.Status == ParticipantStatuses.Invited
                                || p.Status == ParticipantStatuses.Accepted
                                || p.Status == ParticipantStatuses.Assigned))
                .Select(p => p.UserId)
                .ToListAsync(cancellationToken);

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == campusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return departments.Select(d =>
        {
            leadersByDept.TryGetValue(d.DepartmentId, out var deptLeaders);
            // Prefer the configured department head when they are a valid active leader; else first.
            var leader = deptLeaders == null
                ? null
                : (d.HeadUserId.HasValue ? deptLeaders.FirstOrDefault(l => l.UserId == d.HeadUserId.Value) : null)
                  ?? deptLeaders.FirstOrDefault();

            bool canInviteParticipant;
            string? participantDisabledReason;
            bool canReceiveLogistics;
            string? logisticsDisabledReason;
            if (leader == null)
            {
                canInviteParticipant = false;
                participantDisabledReason = "Phòng này chưa có trưởng phòng đang hoạt động.";
                canReceiveLogistics = false;
                logisticsDisabledReason = "Phòng này chưa có trưởng phòng đang hoạt động, không thể nhận yêu cầu hậu cần.";
            }
            else if (alreadyInvitedLeaderIds.Contains(leader.UserId))
            {
                canInviteParticipant = false;
                participantDisabledReason = "Trưởng phòng này đã có trong danh sách tham gia của đoàn.";
                canReceiveLogistics = true;
                logisticsDisabledReason = null;
            }
            else
            {
                canInviteParticipant = true;
                participantDisabledReason = null;
                canReceiveLogistics = true;
                logisticsDisabledReason = null;
            }

            return new SupportDepartmentDto
            {
                DepartmentId = d.DepartmentId,
                DepartmentName = d.Name,
                CampusId = campusId,
                CampusName = campusName,
                LeaderUserId = leader?.UserId,
                LeaderName = leader?.FullName,
                LeaderEmail = leader?.Email,
                CanInviteParticipant = canInviteParticipant,
                ParticipantDisabledReason = participantDisabledReason,
                CanReceiveLogistics = canReceiveLogistics,
                LogisticsDisabledReason = logisticsDisabledReason,
            };
        }).ToList();
    }
}

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
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetAgendaResponsibleCandidates;

public sealed class GetAgendaResponsibleCandidatesQueryHandler
    : IRequestHandler<GetAgendaResponsibleCandidatesQuery, IReadOnlyList<AgendaResponsibleCandidateDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAgendaResponsibleCandidatesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AgendaResponsibleCandidateDto>> Handle(
        GetAgendaResponsibleCandidatesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Relation check — only people involved with the instance may read its candidate list.
        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = _currentUser.RoleCode == RoleCodes.Staff
            && string.Equals(_currentUser.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && _currentUser.PrimaryCampusId == instance.CampusId;
        bool isHo = _currentUser.RoleCode == RoleCodes.Ho;
        bool isAcceptedParticipant = await _db.VisitParticipants.AnyAsync(
            p => p.VisitInstanceId == instance.VisitInstanceId
                 && p.UserId == userId
                 && p.Status == ParticipantStatuses.Accepted,
            cancellationToken);

        if (!(isHost || isStaffLeaderOfCampus || isHo || isAcceptedParticipant))
            throw new ForbiddenException("Bạn không có quyền xem danh sách người phụ trách của chuyến này.");

        var eligible = await ResponsibleCandidateEligibility.GetEligibleUsersAsync(
            _db, instance.VisitInstanceId, instance.CurrentHostUserId, cancellationToken);

        var candidates = eligible.Select(u => new AgendaResponsibleCandidateDto
        {
            UserId = u.UserId,
            FullName = u.FullName,
            Email = u.Email,
            ParticipantRole = u.ParticipantRole!,
            DisplayRole = u.IsMainHost ? "Host chính" : DisplayRoleOf(u.ParticipantRole!),
            IsMainHost = u.IsMainHost,
        }).ToList();

        // Sort: Host chính → IC Support → Department Support → Student Support → full_name.
        return candidates
            .OrderBy(RoleRank)
            .ThenBy(c => c.FullName)
            .ToList();
    }

    private static int RoleRank(AgendaResponsibleCandidateDto c)
    {
        if (c.IsMainHost) return 0;
        return c.ParticipantRole switch
        {
            ParticipantRoles.IcSupport => 1,
            ParticipantRoles.DeptSupport => 2,
            ParticipantRoles.Student => 3,
            _ => 4,
        };
    }

    private static string DisplayRoleOf(string participantRole) => participantRole switch
    {
        ParticipantRoles.IcSupport => "IC Support",
        ParticipantRoles.DeptSupport => "Department Support",
        ParticipantRoles.Student => "Student Support",
        _ => participantRole,
    };
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Queries.GetVisitInvitationDetail;

public sealed class GetVisitInvitationDetailQueryHandler
    : IRequestHandler<GetVisitInvitationDetailQuery, VisitInvitationDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetVisitInvitationDetailQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<VisitInvitationDetailDto> Handle(
        GetVisitInvitationDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode?.ToUpperInvariant();
        var subRole = _currentUser.SubRole?.ToUpperInvariant();

        var query = from p in _context.VisitParticipants
                    join c in _context.VisitRequestCampuses on p.VisitInstanceId equals c.VisitInstanceId
                    join vr in _context.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
                    join user in _context.Users on p.UserId equals user.UserId
                    where p.ParticipantId == request.ParticipantId
                          && p.UserId == userId
                          && !p.IsHost
                          && p.Status != ParticipantStatuses.Removed
                    select new { p, c, vr, user };

        var data = await query.FirstOrDefaultAsync(cancellationToken);
        if (data == null)
            throw new NotFoundException("VisitInvitation", request.ParticipantId);

        var campusName = await _context.Campuses
            .Where(x => x.CampusId == data.c.CampusId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        string invitedByName = "-";
        if (data.p.InvitedBy.HasValue)
        {
            invitedByName = await _context.Users
                .Where(x => x.UserId == data.p.InvitedBy.Value)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken) ?? "-";
        }

        string assignedByName = "-";
        if (data.p.AssignedBy.HasValue)
        {
            assignedByName = await _context.Users
                .Where(x => x.UserId == data.p.AssignedBy.Value)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken) ?? "-";
        }

        var dto = new VisitInvitationDetailDto
        {
            ParticipantId = data.p.ParticipantId,
            VisitInstanceId = data.p.VisitInstanceId,
            VisitRequestId = data.c.VisitRequestId,
            RequestCode = data.vr.RequestCode,
            DelegationName = data.vr.DelegationName,
            OrganizationName = data.vr.RegistrantOrganization,
            CampusName = campusName ?? "-",
            VisitScope = data.vr.VisitScope,
            PlannedStartAt = data.c.PlannedStartAt,
            PlannedEndAt = data.c.PlannedEndAt,
            ParticipantRole = data.p.ParticipantRole,
            InvitationStatus = data.p.Status,
            VisitRequestStatus = data.vr.Status,
            CampusVisitStatus = data.c.Status,
            InvitedByName = invitedByName,
            InvitedAt = data.p.InvitedAt,
            RespondedAt = data.p.RespondedAt,
            Note = data.p.Note,
            DeclineReason = data.p.Status == ParticipantStatuses.Declined ? data.p.Note : null,
            AssignedByName = assignedByName,
            AssignedAt = data.p.AssignedAt,
            AllowedActions = new List<string> { "VIEW_DETAIL" }
        };

        var isClosedOrCancelled = data.vr.Status == VisitRequestStatuses.Cancelled 
            || data.vr.Status == VisitRequestStatuses.Rejected 
            || data.c.Status == VisitInstanceStatuses.Cancelled 
            || data.c.Status == VisitInstanceStatuses.Closed;

        if (roleCode == RoleCodes.Department || roleCode == "DEPT")
        {
            if (subRole == SubRoles.Leader)
            {
                if (data.p.Status == ParticipantStatuses.Invited && !isClosedOrCancelled)
                {
                    dto.AllowedActions.Add("ACCEPT_INVITATION");
                    dto.AllowedActions.Add("DECLINE_INVITATION");
                    dto.AllowedActions.Add("ASSIGN_TO_DEPARTMENT_STAFF");
                }
                else if (data.p.Status == ParticipantStatuses.Accepted && !isClosedOrCancelled)
                {
                    dto.AllowedActions.Add("ASSIGN_TO_DEPARTMENT_STAFF");
                }
            }
        }
        else // Staff, Student
        {
            if (data.p.Status == ParticipantStatuses.Invited && !isClosedOrCancelled)
            {
                dto.AllowedActions.Add("ACCEPT_INVITATION");
                dto.AllowedActions.Add("DECLINE_INVITATION");
            }
        }

        return dto;
    }
}

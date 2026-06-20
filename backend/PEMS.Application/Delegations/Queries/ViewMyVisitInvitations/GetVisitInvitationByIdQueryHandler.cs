using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Queries.ViewMyVisitInvitations;

/// <summary>
/// Returns one invitation for the invitation-detail screen, enforcing ownership: only the
/// invited user may read it (404 otherwise, to avoid leaking existence). The host slot
/// (IC_HOST / is_host) is not an invitation and is never returned here.
/// </summary>
public sealed class GetVisitInvitationByIdQueryHandler
    : IRequestHandler<GetVisitInvitationByIdQuery, VisitInvitationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitInvitationByIdQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<VisitInvitationDto> Handle(
        GetVisitInvitationByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var flat = await (
            from p in _db.VisitParticipants
            join c in _db.VisitRequestCampuses on p.VisitInstanceId equals c.VisitInstanceId
            join vr in _db.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
            where p.ParticipantId == request.ParticipantId
                && p.UserId == userId
                && !p.IsHost
                && p.ParticipantRole != ParticipantRoles.IcHost
            select new VisitInvitationFlat
            {
                ParticipantId = p.ParticipantId,
                VisitInstanceId = p.VisitInstanceId,
                VisitRequestId = c.VisitRequestId,
                CampusId = c.CampusId,
                ParticipantRole = p.ParticipantRole,
                Status = p.Status,
                InvitedByUserId = p.InvitedBy,
                InvitedAt = p.InvitedAt,
                RespondedAt = p.RespondedAt,
                Note = p.Note,
                PlannedStartAt = c.PlannedStartAt,
                PlannedEndAt = c.PlannedEndAt,
                RequestCode = vr.RequestCode,
                DelegationName = vr.DelegationName,
                OrganizationName = vr.RegistrantOrganization,
                Purpose = vr.Purpose,
                WorkingContent = vr.WorkingContent,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("VisitInvitation", request.ParticipantId);

        var dto = VisitInvitationProjection.ToDto(flat);
        var list = new List<VisitInvitationDto> { dto };
        await VisitInvitationProjection.EnrichAsync(_db, list, cancellationToken);
        return dto;
    }
}

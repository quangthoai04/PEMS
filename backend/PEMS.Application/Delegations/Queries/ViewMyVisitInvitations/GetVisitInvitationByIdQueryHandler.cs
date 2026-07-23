using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
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
    private readonly IVisitFormReadService _formReadService;

    public GetVisitInvitationByIdQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
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
                && (p.ParticipantRole == ParticipantRoles.IcSupport
                    || p.ParticipantRole == ParticipantRoles.DeptSupport
                    || p.ParticipantRole == ParticipantRoles.Student)
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
                OrganizationName = vr.RegistrantOrganization,
                // Delegation name / purpose / working content are per-campus and are filled in below from
                // THIS invitation's own instance detail.
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("VisitInvitation", request.ParticipantId);

        // ── Per-campus form v2 (INSTANCE-LEVEL: an invitation is bound to exactly ONE campus instance, so a
        // MIXED request still returns 200 — the delegation name, purpose and working content are sourced ONLY
        // from the TARGET instance's per-campus detail, never the global fields and never a sibling campus.
        // OrganizationName is the registrant organisation (request-level identity) and is unchanged. Missing
        // detail → 409 VISIT_FORM_DETAIL_MISSING, no global fallback. Ownership was enforced in the query
        // above, before this projection. ──
        var visit = await _db.VisitRequests.AsNoTracking()
            .FirstAsync(v => v.VisitRequestId == flat.VisitRequestId, cancellationToken);
        var content = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { flat.VisitInstanceId }, cancellationToken);
        var instanceDetail = content[flat.VisitInstanceId];
        flat.DelegationName = instanceDetail.DelegationName;
        flat.Purpose = instanceDetail.Purpose;
        flat.WorkingContent = instanceDetail.WorkingContent;

        var dto = VisitInvitationProjection.ToDto(flat);
        var list = new List<VisitInvitationDto> { dto };
        await VisitInvitationProjection.EnrichAsync(_db, list, cancellationToken);
        return dto;
    }
}

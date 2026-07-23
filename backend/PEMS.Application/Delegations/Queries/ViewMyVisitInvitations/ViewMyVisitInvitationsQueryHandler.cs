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
/// Lists the signed-in user's participation invitations (UC-27). Rooted on
/// visit_participants (INNER join to instance + request) and projected to flat columns,
/// then enriched in memory — Pomelo/MySQL-friendly (no scalar/correlated subqueries).
/// </summary>
public sealed class ViewMyVisitInvitationsQueryHandler
    : IRequestHandler<ViewMyVisitInvitationsQuery, List<VisitInvitationDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewMyVisitInvitationsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<VisitInvitationDto>> Handle(
        ViewMyVisitInvitationsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var q = from p in _db.VisitParticipants
                join c in _db.VisitRequestCampuses on p.VisitInstanceId equals c.VisitInstanceId
                join vr in _db.VisitRequests on c.VisitRequestId equals vr.VisitRequestId
                where p.UserId == userId
                    && !p.IsHost
                    && (p.ParticipantRole == ParticipantRoles.IcSupport
                        || p.ParticipantRole == ParticipantRoles.DeptSupport
                        || p.ParticipantRole == ParticipantRoles.Student)
                    && vr.Status != VisitRequestStatuses.Rejected
                    && vr.Status != VisitRequestStatuses.Cancelled
                    && c.Status != VisitInstanceStatuses.Cancelled
                select new { p, c, vr };

        if (!request.IncludeResponded)
            q = q.Where(x => x.p.Status == ParticipantStatuses.Invited);
        else
            q = q.Where(x => x.p.Status == ParticipantStatuses.Invited
                          || x.p.Status == ParticipantStatuses.Accepted
                          || x.p.Status == ParticipantStatuses.Assigned
                          || x.p.Status == ParticipantStatuses.Declined);

        var flat = await q
            .OrderByDescending(x => x.p.InvitedAt)
            .ThenByDescending(x => x.p.ParticipantId)
            .Select(x => new VisitInvitationFlat
            {
                ParticipantId = x.p.ParticipantId,
                VisitInstanceId = x.p.VisitInstanceId,
                VisitRequestId = x.c.VisitRequestId,
                CampusId = x.c.CampusId,
                ParticipantRole = x.p.ParticipantRole,
                Status = x.p.Status,
                InvitedByUserId = x.p.InvitedBy,
                InvitedAt = x.p.InvitedAt,
                RespondedAt = x.p.RespondedAt,
                Note = x.p.Note,
                PlannedStartAt = x.c.PlannedStartAt,
                PlannedEndAt = x.c.PlannedEndAt,
                RequestCode = x.vr.RequestCode,
                // An invitation is bound to ONE campus instance, so it always shows THAT instance's own
                // detail — never a sibling campus, never a request-level value.
                DelegationName = x.c.FormDetail != null ? x.c.FormDetail.DelegationName : null,
                OrganizationName = x.vr.RegistrantOrganization,
                Purpose = x.c.FormDetail != null ? x.c.FormDetail.Purpose : null,
                WorkingContent = x.c.FormDetail != null ? x.c.FormDetail.WorkingContent : null,
            })
            .ToListAsync(cancellationToken);

        var items = flat.Select(VisitInvitationProjection.ToDto).ToList();
        await VisitInvitationProjection.EnrichAsync(_db, items, cancellationToken);
        return items;
    }
}

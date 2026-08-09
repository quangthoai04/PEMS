using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// The invitations waiting for the SIGNED-IN person, so somebody who is already using PEMS does not
/// have to go back to their inbox to answer one.
///
/// <para>
/// A pending invitee holds no relation the system has granted yet — they are not the operational
/// contact of anything until they accept — so this is deliberately NOT a widening of the request read
/// scope. <c>VisitFormReadService</c> still refuses them the full request, and an address matching a
/// form field is not evidence of authority. What they get here is the limited summary a person needs
/// in order to decide: which request, which campus, when, who is asking, and how long the invitation
/// lasts. No form content, no sibling campus, no other party's details.
/// </para>
/// <para>
/// Matching is on the account's own verified address against the invitation's normalised target,
/// which is the same test <c>EnsureActorMayTakeContactRole</c> applies before it lets anybody accept —
/// so this list can never offer an invitation the accept would then refuse.
/// </para>
/// </summary>
public sealed class GetMyOperationalContactInvitationsQueryHandler
    : IRequestHandler<GetMyOperationalContactInvitationsQuery, IReadOnlyList<MyOperationalContactInvitationDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public GetMyOperationalContactInvitationsQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _writeFlag = writeFlag;
    }

    public async Task<IReadOnlyList<MyOperationalContactInvitationDto>> Handle(
        GetMyOperationalContactInvitationsQuery request, CancellationToken cancellationToken)
    {
        var empty = (IReadOnlyList<MyOperationalContactInvitationDto>)Array.Empty<MyOperationalContactInvitationDto>();
        if (!_writeFlag.Enabled) return empty;
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null) return empty;

        var email = VisitRequestFingerprintBuilder.NormalizeEmail(_currentUser.Email);
        if (string.IsNullOrWhiteSpace(email)) return empty;

        var now = _clock.VietnamNow;

        // Only invitations that are STILL answerable. An overdue one is not offered even before the
        // sweep settles the row: showing an action the accept would refuse is the drift this exists
        // to remove.
        var rows = await (
            from ch in _db.VisitRequestIdentityChanges.AsNoTracking()
            where ch.Status == IdentityChangeStatuses.Pending
                  && ch.NewEmailNormalized == email
                  && ch.ExpiresAt > now
            join c in _db.VisitRequestCampuses.AsNoTracking() on ch.VisitInstanceId equals c.VisitInstanceId
            join site in _db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            join v in _db.VisitRequests.AsNoTracking() on ch.VisitRequestId equals v.VisitRequestId
            // A cancelled request, or a campus that has been cancelled or refused, has no role left to
            // take on — the accept would refuse it, so it never appears here either.
            where v.Status != VisitRequestStatuses.Cancelled
                  && c.Status != VisitInstanceStatuses.Cancelled
                  && c.Status != VisitInstanceStatuses.Rejected
            orderby ch.ExpiresAt
            select new MyOperationalContactInvitationDto(
                ch.IdentityChangeId,
                ch.VisitRequestId,
                ch.VisitInstanceId,
                ch.ChangeKind,
                v.RequestCode,
                site.Name,
                c.FormDetail!.DelegationName,
                c.PlannedStartAt,
                c.PlannedEndAt,
                // Who is asking. A person deciding whether to take on a campus needs to recognise the
                // request; the registrant's name and organisation are what make an unexpected
                // invitation verifiable. Their address is NOT included — it is not needed to decide.
                v.RegistrantFullName,
                v.RegistrantOrganization,
                ch.ExpiresAt)
        ).ToListAsync(cancellationToken);

        return rows;
    }
}

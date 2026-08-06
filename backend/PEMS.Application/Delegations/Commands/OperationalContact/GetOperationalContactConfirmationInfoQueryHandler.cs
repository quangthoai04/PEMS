using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// Anonymous landing page for a confirmation link. Read-only, and deliberately thin.
///
/// The viewer here is whoever holds the link, which is not yet proof of anything — so the response
/// carries only what somebody deciding whether to accept genuinely needs: the invitation's state, the
/// masked address it was sent to, and the ONE campus involved with its dates. Never the full address,
/// never a sibling campus, never form content, never who the current contact is.
///
/// Every failure returns the same INVALID shape. A forged token, a token for another system, an
/// expired one and a typo are indistinguishable from outside, so the endpoint cannot be used to test
/// whether an address was ever invited to anything.
/// </summary>
public sealed class GetOperationalContactConfirmationInfoQueryHandler
    : IRequestHandler<GetOperationalContactConfirmationInfoQuery, OperationalContactConfirmationInfoResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailActionTokenService _tokens;
    private readonly IDateTimeService _clock;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public GetOperationalContactConfirmationInfoQueryHandler(
        IApplicationDbContext db, IEmailActionTokenService tokens, IDateTimeService clock,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactConfirmationInfoResponse> Handle(
        GetOperationalContactConfirmationInfoQuery request, CancellationToken cancellationToken)
    {
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");

        var invalid = new OperationalContactConfirmationInfoResponse(
            "INVALID", false, null, null, null, null, null, null, null, null,
            RequiresGoogleLoginEmailMatch: true);

        if (string.IsNullOrWhiteSpace(request.Token))
            return invalid;

        var hash = _tokens.Hash(request.Token.Trim());
        var token = await _db.EmailActionTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash
                                      && t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange
                                      && (t.ActionContext == EmailActionContexts.VisitContactClaim
                                          || t.ActionContext == EmailActionContexts.VisitContactTransfer),
                cancellationToken);
        if (token is null)
            return invalid;

        var change = await _db.VisitRequestIdentityChanges.AsNoTracking()
            .Where(c => c.IdentityChangeId == token.TargetId)
            .Select(c => new
            {
                c.ChangeKind,
                c.Status,
                c.NewEmailMasked,
                c.ExpiresAt,
                c.VisitInstanceId,
                c.VisitRequestId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (change is null)
            return invalid;

        // The ONE campus this invitation is about — its own dates and its own delegation name.
        // Joined explicitly: VisitRequestCampus has no Campus navigation.
        var campus = await (
            from c in _db.VisitRequestCampuses.AsNoTracking()
            join site in _db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            where c.VisitInstanceId == change.VisitInstanceId
            select new
            {
                c.PlannedStartAt,
                c.PlannedEndAt,
                CampusName = site.Name,
                DelegationName = c.FormDetail!.DelegationName,
            }).FirstOrDefaultAsync(cancellationToken);

        var requestCode = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == change.VisitRequestId)
            .Select(v => v.RequestCode)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _clock.VietnamNow;

        // Effective state as the viewer experiences it: a PENDING invitation whose window has passed
        // reads EXPIRED here, before the sweep job gets round to settling the row.
        var status = change.Status;
        if (status == IdentityChangeStatuses.Pending
            && (change.ExpiresAt <= now || token.ExpiresAt <= now))
            status = IdentityChangeStatuses.Expired;

        var actionable = status == IdentityChangeStatuses.Pending
                         && token.UsedAt is null
                         && token.ResultStatus == EmailActionResultStatuses.Pending;

        return new OperationalContactConfirmationInfoResponse(
            status,
            actionable,
            change.ChangeKind,
            change.NewEmailMasked,
            requestCode,
            campus?.CampusName,
            campus?.DelegationName,
            campus?.PlannedStartAt,
            campus?.PlannedEndAt,
            status == IdentityChangeStatuses.Pending ? change.ExpiresAt : null,
            RequiresGoogleLoginEmailMatch: true);
    }
}

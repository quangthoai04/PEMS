using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.VisitContactClaim;

/// <summary>
/// Anonymous landing summary for the claim page. Resolves the token by hash, then returns ONLY masked /
/// non-PII fields: the anonymous viewer learns the invitation state, the masked target email and the public
/// request header — never the full email or form content. Gated by the v2 write flag like every claim
/// endpoint (the claim workflow only exists for v2 data).
/// </summary>
public sealed class GetVisitContactClaimInfoQueryHandler
    : IRequestHandler<GetVisitContactClaimInfoQuery, VisitContactClaimInfoResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailActionTokenService _tokens;
    private readonly IDateTimeService _clock;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public GetVisitContactClaimInfoQueryHandler(
        IApplicationDbContext db, IEmailActionTokenService tokens, IDateTimeService clock,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _writeFlag = writeFlag;
    }

    public async Task<VisitContactClaimInfoResponse> Handle(
        GetVisitContactClaimInfoQuery request, CancellationToken cancellationToken)
    {
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");

        var invalid = new VisitContactClaimInfoResponse(
            "INVALID", false, null, null, null, null, null, RequiresGoogleLoginEmailMatch: true);

        if (string.IsNullOrWhiteSpace(request.Token))
            return invalid;

        var hash = _tokens.Hash(request.Token.Trim());
        var token = await _db.EmailActionTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash
                                      && t.ActionContext == EmailActionContexts.VisitContactClaim
                                      && t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange,
                cancellationToken);
        if (token is null)
            return invalid;

        var claim = await _db.VisitRequestIdentityChanges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentityChangeId == token.TargetId, cancellationToken);
        if (claim is null)
            return invalid;

        var head = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == claim.VisitRequestId)
            .Select(v => new { v.RequestCode, v.DelegationName, v.RegistrantFullName })
            .FirstOrDefaultAsync(cancellationToken);

        var now = _clock.VietnamNow;
        // Effective state as the viewer experiences it: a PENDING claim whose token/claim expired is EXPIRED.
        var status = claim.Status;
        if (status == IdentityChangeStatuses.Pending && (claim.ExpiresAt <= now || token.ExpiresAt <= now))
            status = IdentityChangeStatuses.Expired;

        var actionable = status == IdentityChangeStatuses.Pending
                         && token.UsedAt is null
                         && token.ResultStatus == EmailActionResultStatuses.Pending;

        return new VisitContactClaimInfoResponse(
            status,
            actionable,
            claim.NewEmailMasked,
            head?.DelegationName,
            head?.RequestCode,
            head?.RegistrantFullName,
            claim.Status == IdentityChangeStatuses.Pending ? claim.ExpiresAt : null,
            RequiresGoogleLoginEmailMatch: true);
    }
}

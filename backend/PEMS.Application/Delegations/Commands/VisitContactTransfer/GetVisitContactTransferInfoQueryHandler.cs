using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.VisitContactTransfer;

/// <summary>
/// Anonymous masked landing for a transfer link. Read-only, mutation-free and enumeration-safe: an
/// unknown/foreign token returns the same INVALID shape as a malformed one, and the response never
/// carries the full invited email, the current owner's email or any form PII.
/// </summary>
public sealed class GetVisitContactTransferInfoQueryHandler
    : IRequestHandler<GetVisitContactTransferInfoQuery, VisitContactTransferInfoResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailActionTokenService _tokens;
    private readonly IDateTimeService _clock;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public GetVisitContactTransferInfoQueryHandler(
        IApplicationDbContext db, IEmailActionTokenService tokens, IDateTimeService clock,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _writeFlag = writeFlag;
    }

    public async Task<VisitContactTransferInfoResponse> Handle(
        GetVisitContactTransferInfoQuery request, CancellationToken cancellationToken)
    {
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");

        var invalid = new VisitContactTransferInfoResponse(
            "INVALID", false, null, null, null, null, null, RequiresGoogleLoginEmailMatch: true);

        if (string.IsNullOrWhiteSpace(request.Token))
            return invalid;

        var hash = _tokens.Hash(request.Token.Trim());
        var token = await _db.EmailActionTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash
                                      && t.ActionContext == EmailActionContexts.VisitContactTransfer
                                      && t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange,
                cancellationToken);
        if (token is null)
            return invalid;

        var transfer = await _db.VisitRequestIdentityChanges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentityChangeId == token.TargetId
                                      && c.ChangeKind == IdentityChangeKinds.Transfer, cancellationToken);
        if (transfer is null)
            return invalid;

        var head = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == transfer.VisitRequestId)
            .Select(v => new { v.RequestCode, v.DelegationName })
            .FirstOrDefaultAsync(cancellationToken);
        var requesterName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == transfer.RequestedBy)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _clock.VietnamNow;
        var status = transfer.Status;
        if (status == IdentityChangeStatuses.Pending && (transfer.ExpiresAt <= now || token.ExpiresAt <= now))
            status = IdentityChangeStatuses.Expired;

        var actionable = status == IdentityChangeStatuses.Pending
                         && token.UsedAt is null
                         && token.ResultStatus == EmailActionResultStatuses.Pending;

        return new VisitContactTransferInfoResponse(
            status,
            actionable,
            transfer.NewEmailMasked,
            head?.DelegationName,
            head?.RequestCode,
            requesterName,
            transfer.Status == IdentityChangeStatuses.Pending ? transfer.ExpiresAt : null,
            RequiresGoogleLoginEmailMatch: true);
    }
}

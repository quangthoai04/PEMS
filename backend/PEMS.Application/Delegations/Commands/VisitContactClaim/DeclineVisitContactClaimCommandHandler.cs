using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.VisitContactClaim;

/// <summary>
/// The invited contact explicitly DECLINES the INITIAL_CLAIM. Same authentication bar as accept (the claim
/// is a grant of authority — only the invited email may answer it either way). The request itself is NOT
/// cancelled: the contact stays PENDING_CONFIRMATION with no owner, the registrant remains the verified
/// editor and can resend or replace the contact. Terminal state gets a 90-day retention stamp for the
/// redaction job (plan §16.8).
/// </summary>
public sealed class DeclineVisitContactClaimCommandHandler
    : IRequestHandler<DeclineVisitContactClaimCommand, VisitContactClaimActionResponse>
{
    private const int RetentionDays = 90;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailActionTokenService _tokens;
    private readonly IVisitContactClaimService _claimService;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public DeclineVisitContactClaimCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IEmailActionTokenService tokens, IVisitContactClaimService claimService,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _tokens = tokens;
        _claimService = claimService;
        _writeFlag = writeFlag;
    }

    public async Task<VisitContactClaimActionResponse> Handle(
        DeclineVisitContactClaimCommand request, CancellationToken cancellationToken)
    {
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaimErrorCodes.TokenInvalid);

        var hash = _tokens.Hash(request.Token.Trim());
        var claimId = await _db.EmailActionTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash
                        && t.ActionContext == EmailActionContexts.VisitContactClaim
                        && t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange)
            .Select(t => (ulong?)t.TargetId)
            .FirstOrDefaultAsync(cancellationToken);
        if (claimId is null)
            throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaimErrorCodes.TokenInvalid);

        var actorId = _currentUser.UserId.Value;
        var now = _clock.VietnamNow;

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var (claim, token, visit, actor) = await AcceptVisitContactClaimCommandHandler
            .LoadLockedForClaimActionAsync(
                _db, _tokens, _clock, _claimService, claimId.Value, request.Token, actorId, cancellationToken);

        var actorEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(actor.Email);
        if (actorEmailNorm != claim.NewEmailNormalized)
            throw new ConflictException(
                "Tài khoản đang đăng nhập không trùng với email được mời.",
                VisitContactClaimErrorCodes.EmailMismatch);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        claim.Status = IdentityChangeStatuses.Declined;
        claim.DeclinedAt = now;
        claim.Reason = reason;
        claim.RetentionUntil = now.AddDays(RetentionDays);
        claim.UpdatedAt = now;

        token.UsedAt = now;
        token.UsedAction = EmailIntendedActions.Decline;
        token.ResultStatus = EmailActionResultStatuses.Success;
        token.RecipientUserId = actorId;

        await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitRequestIdentityChange, claim.IdentityChangeId,
            "Lời mời đã bị từ chối.", now, cancellationToken);

        var correlationId = Guid.NewGuid().ToString("N");
        _db.VisitRequestIdentityChangeEvents.Add(new Domain.Entities.Delegations.VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = claim.IdentityChangeId,
            VisitRequestId = visit.VisitRequestId,
            EventType = "PRIMARY_CONTACT_INVITATION_DECLINED",
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Declined,
            ActorUserId = actorId,
            EmailMasked = claim.NewEmailMasked,
            Reason = reason,
            CorrelationId = correlationId,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "PRIMARY_CONTACT_INVITATION_DECLINED",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            VisitRequestId = visit.VisitRequestId,
            CorrelationId = correlationId,
            SourceType = "IDENTITY",
            Reason = reason,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new VisitContactClaimActionResponse(
            visit.VisitRequestId, visit.RequestCode ?? string.Empty,
            IdentityChangeStatuses.Declined, visit.PrimaryContactAccessStatus,
            "Bạn đã từ chối lời mời. Đơn đăng ký không bị hủy; người đăng ký có thể chỉ định đầu mối khác.");
    }
}

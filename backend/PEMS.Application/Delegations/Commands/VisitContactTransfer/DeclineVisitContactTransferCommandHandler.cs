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

namespace PEMS.Application.Delegations.Commands.VisitContactTransfer;

/// <summary>
/// The invited person DECLINES the 24h transfer. Only the exact invited account may answer (same bar as
/// accept). The current owner keeps everything — no request relation, approval or snapshot changes;
/// the transfer becomes terminal DECLINED with a 90-day retention stamp for the redaction job.
/// </summary>
public sealed class DeclineVisitContactTransferCommandHandler
    : IRequestHandler<DeclineVisitContactTransferCommand, VisitContactTransferActionResponse>
{
    private const int RetentionDays = 90;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailActionTokenService _tokens;
    private readonly IVisitContactClaimService _claimService;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public DeclineVisitContactTransferCommandHandler(
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

    public async Task<VisitContactTransferActionResponse> Handle(
        DeclineVisitContactTransferCommand request, CancellationToken cancellationToken)
    {
        var (transferId, actorId) = await TransferTokenGuards.ResolveAsync(
            _writeFlag, _currentUser, _tokens, _db, request.Token, cancellationToken);
        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var transfer = await _claimService.LockClaimAsync(transferId, cancellationToken)
            ?? throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaim.VisitContactClaimErrorCodes.TokenInvalid);
        if (transfer.ChangeKind != IdentityChangeKinds.Transfer)
            throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaim.VisitContactClaimErrorCodes.TokenInvalid);
        TransferTokenGuards.EnsurePendingTransferState(transfer, now);
        var token = await TransferTokenGuards.LoadLiveTokenAsync(
            _db, _tokens, request.Token, transferId, now, cancellationToken);

        var actor = await _db.Users.FirstOrDefaultAsync(u => u.UserId == actorId, cancellationToken)
            ?? throw new ForbiddenException();
        var actorEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(actor.Email);
        if (actorEmailNorm != transfer.NewEmailNormalized)
            throw new ConflictException(
                "Tài khoản đang đăng nhập không trùng với email được mời.",
                VisitContactTransferErrorCodes.GoogleEmailMismatch);

        var visit = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == transfer.VisitRequestId)
            .Select(v => new { v.VisitRequestId, v.RequestCode, v.PrimaryContactAccessStatus })
            .FirstAsync(cancellationToken);

        transfer.Status = IdentityChangeStatuses.Declined;
        transfer.DeclinedAt = now;
        transfer.Reason = reason;
        transfer.RetentionUntil = now.AddDays(RetentionDays);
        transfer.UpdatedAt = now;

        token.UsedAt = now;
        token.UsedAction = EmailIntendedActions.Decline;
        token.ResultStatus = EmailActionResultStatuses.Success;
        token.RecipientUserId = actorId;
        await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitRequestIdentityChange, transfer.IdentityChangeId,
            "Lời mời chuyển giao đã bị từ chối.", now, cancellationToken);

        _db.VisitRequestIdentityChangeEvents.Add(new Domain.Entities.Delegations.VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = transfer.IdentityChangeId,
            VisitRequestId = transfer.VisitRequestId,
            EventType = "PRIMARY_CONTACT_TRANSFER_DECLINED",
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Declined,
            ActorUserId = actorId,
            EmailMasked = transfer.NewEmailMasked,
            Reason = reason,
            CorrelationId = correlationId,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "PRIMARY_CONTACT_TRANSFER_DECLINED",
            EntityType = "VisitRequest",
            EntityId = transfer.VisitRequestId,
            VisitRequestId = transfer.VisitRequestId,
            CorrelationId = correlationId,
            SourceType = "IDENTITY",
            Reason = reason,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new VisitContactTransferActionResponse(
            visit.VisitRequestId, visit.RequestCode ?? string.Empty,
            IdentityChangeStatuses.Declined, visit.PrimaryContactAccessStatus,
            Idempotent: false,
            "Bạn đã từ chối lời mời chuyển giao. Đầu mối hiện tại giữ nguyên quyền quản lý đơn.");
    }
}

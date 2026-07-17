using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.VisitContactTransfer;

/// <summary>
/// Owner-side transfer state view (registrant or current ACTIVE primary contact). Masked email only —
/// even the managers never read the full invited email back out of the API.
/// </summary>
public sealed class GetActiveVisitContactTransferQueryHandler
    : IRequestHandler<GetActiveVisitContactTransferQuery, VisitContactTransferStateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public GetActiveVisitContactTransferQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _writeFlag = writeFlag;
    }

    public async Task<VisitContactTransferStateResponse> Handle(
        GetActiveVisitContactTransferQuery request, CancellationToken cancellationToken)
    {
        var actorId = TransferGuards.EnsureAuthenticatedVisitor(_writeFlag, _currentUser);

        var visit = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == request.VisitRequestId)
            .Select(v => new { v.VisitRequestId, v.RegistrantUserId, v.VisitorUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);
        if (visit.RegistrantUserId != actorId && visit.VisitorUserId != actorId)
            throw new ForbiddenException("Chỉ người đăng ký hoặc đầu mối liên hệ hiện tại mới xem được trạng thái chuyển giao.");

        var now = _clock.VietnamNow;
        var pending = await _db.VisitRequestIdentityChanges.AsNoTracking()
            .Where(c => c.VisitRequestId == request.VisitRequestId
                        && c.ChangeKind == IdentityChangeKinds.Transfer
                        && c.Status == IdentityChangeStatuses.Pending)
            .Select(c => new { c.IdentityChangeId, c.NewEmailMasked, c.ExpiresAt, c.ResendCount })
            .FirstOrDefaultAsync(cancellationToken);

        if (pending is null)
            return new VisitContactTransferStateResponse(
                request.VisitRequestId, false, null, null, null, null, 0);

        // Overdue-but-not-yet-swept rows read as EXPIRED (the job settles them asynchronously).
        var effectiveStatus = pending.ExpiresAt <= now
            ? IdentityChangeStatuses.Expired
            : IdentityChangeStatuses.Pending;
        return new VisitContactTransferStateResponse(
            request.VisitRequestId,
            HasPendingTransfer: effectiveStatus == IdentityChangeStatuses.Pending,
            pending.IdentityChangeId, effectiveStatus, pending.NewEmailMasked,
            pending.ExpiresAt, pending.ResendCount);
    }
}

/// <summary>
/// Re-sends the pending TRANSFER invitation (registrant or current ACTIVE owner). Old links die FIRST,
/// the 24h window restarts, and <c>expected_request_row_version</c> is re-stamped to the CURRENT request
/// version — a legitimate edit between invitations therefore never bricks the accept, while an edit AFTER
/// the latest invitation still forces a re-invite (accept re-checks the stamp).
/// </summary>
public sealed class ResendVisitContactTransferCommandHandler
    : IRequestHandler<ResendVisitContactTransferCommand, VisitContactTransferManageResponse>
{
    private const int MaxResends = 5;
    private const int TransferValidityHours = 24;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitContactClaimService _claimService;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public ResendVisitContactTransferCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitContactClaimService claimService, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _claimService = claimService;
        _writeFlag = writeFlag;
    }

    public async Task<VisitContactTransferManageResponse> Handle(
        ResendVisitContactTransferCommand request, CancellationToken cancellationToken)
    {
        var actorId = TransferGuards.EnsureAuthenticatedVisitor(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;

        ulong transferId;
        VisitRequest visit;
        VisitRequestIdentityChange transfer;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            visit = await TransferGuards.LoadRequestForTransferActorAsync(
                _db, request.VisitRequestId, actorId, cancellationToken);
            TransferGuards.EnsureTransferLifecycleOpen(visit, now);

            transfer = await _claimService.LockPendingChangeAsync(
                    visit.VisitRequestId, IdentityChangeKinds.Transfer, cancellationToken)
                ?? throw new ConflictException(
                    "Không có lời mời chuyển giao nào đang chờ xác nhận để gửi lại.",
                    VisitContactTransferErrorCodes.NonePending);
            if (transfer.ExpiresAt <= now)
                throw new ConflictException(
                    "Lời mời chuyển giao đã hết hạn. Vui lòng tạo lời mời mới.",
                    VisitContactTransferErrorCodes.Expired);
            if (transfer.ResendCount >= MaxResends)
                throw new BusinessRuleException(
                    "Đã vượt quá số lần gửi lại lời mời chuyển giao cho đơn này.",
                    VisitContactTransferErrorCodes.ResendLimit);

            await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _db, EmailActionTargetTypes.VisitRequestIdentityChange, transfer.IdentityChangeId,
                "Lời mời chuyển giao đã được gửi lại bằng liên kết mới.", now, cancellationToken);

            transfer.ResendCount += 1;
            transfer.ExpiresAt = now.AddHours(TransferValidityHours);
            transfer.ExpectedRequestRowVersion = (uint)visit.RowVersion; // re-stamp → accept stays possible
            transfer.UpdatedAt = now;

            var correlationId = Guid.NewGuid().ToString("N");
            _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
            {
                IdentityChangeId = transfer.IdentityChangeId,
                VisitRequestId = visit.VisitRequestId,
                EventType = "PRIMARY_CONTACT_TRANSFER_RESENT",
                FromStatus = IdentityChangeStatuses.Pending,
                ToStatus = IdentityChangeStatuses.Pending,
                ActorUserId = actorId,
                EmailMasked = transfer.NewEmailMasked,
                Reason = $"resend_count={transfer.ResendCount};new_expiry={transfer.ExpiresAt:yyyy-MM-dd HH:mm}",
                CorrelationId = correlationId,
                CreatedAt = now,
            });
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                Action = "PRIMARY_CONTACT_TRANSFER_RESENT",
                EntityType = "VisitRequest",
                EntityId = visit.VisitRequestId,
                VisitRequestId = visit.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            transferId = transfer.IdentityChangeId;
        }

        await _claimService.SendTransferInvitationAsync(transferId, cancellationToken);

        return new VisitContactTransferManageResponse(
            visit.VisitRequestId, IdentityChangeStatuses.Pending, transfer.NewEmailMasked,
            transfer.ExpiresAt, transfer.ResendCount,
            "Đã gửi lại lời mời chuyển giao. Liên kết cũ không còn hiệu lực.");
    }
}

/// <summary>
/// Cancels the pending TRANSFER (registrant or current ACTIVE owner). The current owner stays ACTIVE —
/// this only closes the invitation and burns its links.
/// </summary>
public sealed class CancelVisitContactTransferCommandHandler
    : IRequestHandler<CancelVisitContactTransferCommand, VisitContactTransferManageResponse>
{
    private const int RetentionDays = 90;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitContactClaimService _claimService;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public CancelVisitContactTransferCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitContactClaimService claimService, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _claimService = claimService;
        _writeFlag = writeFlag;
    }

    public async Task<VisitContactTransferManageResponse> Handle(
        CancelVisitContactTransferCommand request, CancellationToken cancellationToken)
    {
        var actorId = TransferGuards.EnsureAuthenticatedVisitor(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var visit = await TransferGuards.LoadRequestForTransferActorAsync(
            _db, request.VisitRequestId, actorId, cancellationToken);

        var transfer = await _claimService.LockPendingChangeAsync(
                visit.VisitRequestId, IdentityChangeKinds.Transfer, cancellationToken)
            ?? throw new ConflictException(
                "Không có lời mời chuyển giao nào đang chờ xác nhận để hủy.",
                VisitContactTransferErrorCodes.NonePending);

        await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitRequestIdentityChange, transfer.IdentityChangeId,
            "Lời mời chuyển giao đã bị hủy.", now, cancellationToken);

        transfer.Status = IdentityChangeStatuses.Cancelled;
        transfer.CancelledAt = now;
        transfer.Reason = reason ?? transfer.Reason;
        transfer.RetentionUntil = now.AddDays(RetentionDays);
        transfer.UpdatedAt = now;

        var correlationId = Guid.NewGuid().ToString("N");
        _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = transfer.IdentityChangeId,
            VisitRequestId = visit.VisitRequestId,
            EventType = "PRIMARY_CONTACT_TRANSFER_CANCELLED",
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Cancelled,
            ActorUserId = actorId,
            EmailMasked = transfer.NewEmailMasked,
            Reason = reason,
            CorrelationId = correlationId,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "PRIMARY_CONTACT_TRANSFER_CANCELLED",
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

        return new VisitContactTransferManageResponse(
            visit.VisitRequestId, IdentityChangeStatuses.Cancelled, transfer.NewEmailMasked,
            null, transfer.ResendCount,
            "Đã hủy lời mời chuyển giao. Đầu mối hiện tại giữ nguyên quyền.");
    }
}

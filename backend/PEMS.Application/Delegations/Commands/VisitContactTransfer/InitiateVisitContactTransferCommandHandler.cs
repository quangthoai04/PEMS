using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.VisitContactTransfer;

/// <summary>
/// Initiates the 24h primary-contact TRANSFER (plan §16.4/§4.4, handoff §6.3): the REGISTRANT or the
/// current ACTIVE primary contact proposes handing the contact role to a new email. NOTHING changes for
/// the current owner here — they stay ACTIVE with full rights until the invited person logs in with the
/// matching Google account and explicitly accepts. Uses the same identity-change row/token store as the
/// INITIAL_CLAIM, with <c>change_kind=TRANSFER</c>, a captured old-owner snapshot and a 24h expiry.
/// The DB pending-guard (one PENDING change per request/relation) plus a FOR-UPDATE pre-check make
/// concurrent initiates one-winner; the loser gets a stable 409.
/// </summary>
public sealed class InitiateVisitContactTransferCommandHandler
    : IRequestHandler<InitiateVisitContactTransferCommand, VisitContactTransferManageResponse>
{
    private const int TransferValidityHours = 24;
    private const int SelfServiceCutoffHours = 24;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitContactClaimService _claimService;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public InitiateVisitContactTransferCommandHandler(
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
        InitiateVisitContactTransferCommand request, CancellationToken cancellationToken)
    {
        var actorId = TransferGuards.EnsureAuthenticatedVisitor(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");

        var newEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(request.Email);
        var newEmailMasked = VisitRequestFingerprintBuilder.MaskEmail(newEmailNorm);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        ulong transferId;
        VisitRequest visit;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            visit = await TransferGuards.LoadRequestForTransferActorAsync(
                _db, request.VisitRequestId, actorId, cancellationToken);
            TransferGuards.EnsureTransferLifecycleOpen(visit, now);

            // ── Target email rules ──
            var oldEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(visit.ContactPersonEmail);
            if (newEmailNorm == oldEmailNorm)
                throw new BusinessRuleException(
                    "Email mới trùng với email đầu mối hiện tại.",
                    VisitContactTransferErrorCodes.EmailUnchanged);

            var targetAccount = await _db.Users.AsNoTracking()
                .Where(u => u.Email == newEmailNorm)
                .Select(u => new { u.UserId, RoleCode = u.Role.RoleCode, u.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (targetAccount is not null && targetAccount.RoleCode != RoleCodes.Visitor)
                throw new ConflictException(
                    "Email này thuộc tài khoản nội bộ nên không thể làm đầu mối liên hệ khách.",
                    VisitContactTransferErrorCodes.InternalAccountConflict);
            if (targetAccount is not null && targetAccount.Status != UserStatuses.Active)
                throw new ConflictException(
                    "Tài khoản khách của email này đang không hoạt động nên không thể nhận vai trò đầu mối.",
                    VisitContactTransferErrorCodes.TargetNotAllowed);

            // ── One PENDING identity change per request (claim OR transfer): lock + pre-check; the DB
            //    unique pending-guard is the last line for a true race (mapped to the same 409 below). ──
            var pending = await _claimService.LockPendingChangeAsync(
                visit.VisitRequestId, changeKind: null, cancellationToken);
            if (pending is not null)
                throw new ConflictException(
                    "Đơn này đã có một thay đổi đầu mối đang chờ xác nhận. Vui lòng hủy/hoàn tất thay đổi đó trước.",
                    VisitContactTransferErrorCodes.AlreadyPending);

            var transfer = new VisitRequestIdentityChange
            {
                VisitRequestId = visit.VisitRequestId,
                ChangeKind = IdentityChangeKinds.Transfer,
                TargetRelation = IdentityChangeTargetRelations.PrimaryContact,
                ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
                OldUserId = visit.VisitorUserId,          // TRANSFER always captures the current owner
                OldEmailNormalized = oldEmailNorm,
                NewUserId = null,
                NewEmailNormalized = newEmailNorm,
                NewEmailMasked = newEmailMasked,
                PendingSnapshotJson = JsonSerializer.Serialize(new
                {
                    fullName = request.FullName.Trim(),
                    organization = string.IsNullOrWhiteSpace(request.Organization) ? null : request.Organization.Trim(),
                    phone = request.Phone.Trim(),
                    email = newEmailNorm,
                }, Json),
                Status = IdentityChangeStatuses.Pending,
                ExpectedRequestRowVersion = (uint)visit.RowVersion,
                RequestedBy = actorId,
                RequestedAt = now,
                ExpiresAt = now.AddHours(TransferValidityHours), // 24h — NOT the 72h claim window
                Reason = reason,
                ResendCount = 0,
                CreatedAt = now,
            };
            _db.VisitRequestIdentityChanges.Add(transfer);

            try
            {
                await _db.SaveChangesAsync(cancellationToken); // resolve id (also fires the DB pending-guard)
            }
            catch (DbUpdateException)
            {
                // Concurrent initiate lost the unique pending-guard race → same stable 409 as the pre-check.
                await tx.RollbackAsync(cancellationToken);
                throw new ConflictException(
                    "Đơn này đã có một thay đổi đầu mối đang chờ xác nhận. Vui lòng hủy/hoàn tất thay đổi đó trước.",
                    VisitContactTransferErrorCodes.AlreadyPending);
            }

            _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
            {
                IdentityChangeId = transfer.IdentityChangeId,
                VisitRequestId = visit.VisitRequestId,
                EventType = "PRIMARY_CONTACT_TRANSFER_REQUESTED",
                FromStatus = null,
                ToStatus = IdentityChangeStatuses.Pending,
                ActorUserId = actorId,
                EmailMasked = newEmailMasked,
                Reason = reason,
                CorrelationId = correlationId,
                CreatedAt = now,
            });
            var audit = new AuditLog
            {
                ActorUserId = actorId,
                Action = "PRIMARY_CONTACT_TRANSFER_REQUESTED",
                EntityType = "VisitRequest",
                EntityId = visit.VisitRequestId,
                VisitRequestId = visit.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                Reason = reason,
                CreatedAt = now,
            };
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "primary_contact_transfer_target",
                OldValueText = VisitRequestFingerprintBuilder.MaskEmail(oldEmailNorm), // masked only
                NewValueText = newEmailMasked,
                CreatedAt = now,
            });
            _db.AuditLogs.Add(audit);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            transferId = transfer.IdentityChangeId;
        }

        // AFTER commit: mint token + send the 24h invitation (best-effort; resend recovers).
        await _claimService.SendTransferInvitationAsync(transferId, cancellationToken);

        return new VisitContactTransferManageResponse(
            visit.VisitRequestId, IdentityChangeStatuses.Pending, newEmailMasked,
            now.AddHours(TransferValidityHours), 0,
            "Đã gửi lời mời chuyển giao vai trò đầu mối. Đầu mối hiện tại giữ nguyên quyền cho tới khi người mới xác nhận.");
    }
}

/// <summary>Shared guards for the transfer commands (initiate / resend / cancel / view).</summary>
internal static class TransferGuards
{
    public static ulong EnsureAuthenticatedVisitor(
        PerCampusFormV2WriteOptions writeFlag, ICurrentUserService currentUser)
    {
        if (!writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            throw new ForbiddenException();
        if (currentUser.RoleCode != RoleCodes.Visitor)
            throw new ForbiddenException("Chỉ khách (Visitor) mới được quản lý chuyển giao đầu mối.");
        return currentUser.UserId.Value;
    }

    /// <summary>
    /// Loads the request (tracked) and enforces the transfer actor policy: REGISTRANT or the CURRENT
    /// ACTIVE primary contact of THIS request; the request must be per-campus v2 with an established
    /// (ACTIVE, linked) contact — an unclaimed contact is INITIAL_CLAIM territory (resend/replace).
    /// </summary>
    public static async Task<VisitRequest> LoadRequestForTransferActorAsync(
        IApplicationDbContext db, ulong visitRequestId, ulong actorId, CancellationToken ct)
    {
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == visitRequestId, ct)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", visitRequestId);

        if (visit.FormSchemaVersion < FormSchemaVersions.PerCampus)
            throw new BusinessRuleException(
                "Đơn này không dùng biểu mẫu theo cơ sở (v2).",
                VisitFormV2ErrorCodes.FormVersionUpgradeRequired);
        if (visit.RegistrantUserId != actorId && visit.VisitorUserId != actorId)
            throw new ForbiddenException(
                "Chỉ người đăng ký hoặc đầu mối liên hệ hiện tại mới được quản lý chuyển giao đầu mối.");
        if (visit.PrimaryContactAccessStatus != PrimaryContactAccessStatuses.Active
            || visit.VisitorUserId is null)
            throw new ConflictException(
                "Đầu mối hiện tại chưa xác nhận (chưa ACTIVE) nên chưa thể chuyển giao. Hãy dùng gửi lại/đổi lời mời ban đầu.",
                VisitContactTransferErrorCodes.ContactNotActive);
        return visit;
    }

    /// <summary>
    /// Self-service transfer window (handoff §6.3): blocked once the request is cancelled, once ANY
    /// campus instance has started (DURING_VISIT / AFTER_VISIT / CLOSED), or when the earliest still-active
    /// planned start is less than 24h away.
    /// </summary>
    public static void EnsureTransferLifecycleOpen(VisitRequest visit, DateTime vnNow)
    {
        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new BusinessRuleException(
                "Đơn đã bị hủy nên không thể chuyển giao đầu mối.",
                VisitContactTransferErrorCodes.Conflict);
        if (visit.CampusInstances.Any(c =>
                c.Status == VisitInstanceStatuses.DuringVisit
                || c.Status == VisitInstanceStatuses.AfterVisit
                || c.Status == VisitInstanceStatuses.Closed))
            throw new BusinessRuleException(
                "Chuyến thăm đã/đang diễn ra nên không thể tự chuyển giao đầu mối. Vui lòng liên hệ FPTU để được hỗ trợ.",
                VisitContactTransferErrorCodes.Conflict);
        var activeStarts = visit.CampusInstances
            .Where(c => c.Status != VisitInstanceStatuses.Cancelled && c.Status != VisitInstanceStatuses.Rejected)
            .Select(c => c.PlannedStartAt)
            .ToList();
        if (activeStarts.Count > 0 && activeStarts.Min() < vnNow.AddHours(24))
            throw new BusinessRuleException(
                "Lịch thăm bắt đầu trong vòng 24 giờ nên không thể tự chuyển giao đầu mối. Vui lòng liên hệ FPTU để được hỗ trợ.",
                VisitContactTransferErrorCodes.Conflict);
    }
}

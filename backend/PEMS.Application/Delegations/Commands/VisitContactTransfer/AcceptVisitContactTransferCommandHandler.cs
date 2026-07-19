using System;
using System.Text.Json;
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
/// Applies the 24h primary-contact TRANSFER (handoff §6.4). Only the invited person — logged in with the
/// Google account whose email equals <c>new_email_normalized</c> — can accept; the swap of
/// <c>visitor_user_id</c> + the contact snapshot happens in the SAME transaction as PENDING → APPLIED on
/// the FOR-UPDATE-locked change row. The old owner keeps every right until this commit and their account is
/// never locked/deleted — only the request relation moves. Campus status/decisions/host/schedule/revisions
/// are never touched. A post-commit retry by the same accepted user replays idempotently; a concurrent
/// accept has exactly one winner (row lock), the loser sees a non-PENDING state.
/// </summary>
public sealed class AcceptVisitContactTransferCommandHandler
    : IRequestHandler<AcceptVisitContactTransferCommand, VisitContactTransferActionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailActionTokenService _tokens;
    private readonly IVisitContactClaimService _claimService;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public AcceptVisitContactTransferCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IEmailActionTokenService tokens, IVisitContactClaimService claimService,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _tokens = tokens;
        _claimService = claimService;
        _notificationService = notificationService;
        _writeFlag = writeFlag;
    }

    public async Task<VisitContactTransferActionResponse> Handle(
        AcceptVisitContactTransferCommand request, CancellationToken cancellationToken)
    {
        var (transferId, actorId) = await TransferTokenGuards.ResolveAsync(
            _writeFlag, _currentUser, _tokens, _db, request.Token, cancellationToken);
        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");

        ulong visitRequestId;
        string requestCode;
        ulong? oldOwnerId;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var transfer = await _claimService.LockClaimAsync(transferId, cancellationToken)
                ?? throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaim.VisitContactClaimErrorCodes.TokenInvalid);
            if (transfer.ChangeKind != IdentityChangeKinds.Transfer)
                throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaim.VisitContactClaimErrorCodes.TokenInvalid);

            var actor = await _db.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == actorId, cancellationToken)
                ?? throw new ForbiddenException();

            // ── Post-commit idempotent replay: the SAME accepted user retries → the applied result,
            //    never a second swap. Anyone else on a settled transfer gets the state error below. ──
            if (transfer.Status == IdentityChangeStatuses.Applied && transfer.NewUserId == actorId)
            {
                var appliedVisit = await _db.VisitRequests.AsNoTracking()
                    .Where(v => v.VisitRequestId == transfer.VisitRequestId)
                    .Select(v => new { v.VisitRequestId, v.RequestCode, v.PrimaryContactAccessStatus })
                    .FirstAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return new VisitContactTransferActionResponse(
                    appliedVisit.VisitRequestId, appliedVisit.RequestCode ?? string.Empty,
                    IdentityChangeStatuses.Applied, appliedVisit.PrimaryContactAccessStatus,
                    Idempotent: true, "Bạn đã là đầu mối liên hệ của đơn này.");
            }

            TransferTokenGuards.EnsurePendingTransferState(transfer, now);

            var token = await TransferTokenGuards.LoadLiveTokenAsync(
                _db, _tokens, request.Token, transferId, now, cancellationToken);

            // ── Invited-email + account bar (same as the claim flow) ──
            if (actor.Role?.RoleCode != RoleCodes.Visitor)
                throw new ConflictException(
                    "Tài khoản nội bộ không thể nhận vai trò đầu mối liên hệ khách.",
                    VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount);
            if (actor.Status != UserStatuses.Active)
                throw new BusinessRuleException(
                    "Tài khoản khách này đang không hoạt động.",
                    VisitContactTransferErrorCodes.TargetNotAllowed);
            var actorEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(actor.Email);
            if (actorEmailNorm != transfer.NewEmailNormalized)
                throw new ConflictException(
                    "Tài khoản đang đăng nhập không trùng với email được mời. Vui lòng đăng nhập đúng tài khoản Google của email nhận lời mời.",
                    VisitContactTransferErrorCodes.GoogleEmailMismatch);

            // ── Request-side re-checks: the owner must still be exactly the captured old owner and the
            //    lifecycle window must still be open. expected_request_row_version is re-stamped on every
            //    resend, so a mismatch means the request changed since the last invitation → re-invite. ──
            var visit = await _db.VisitRequests
                .Include(v => v.CampusInstances)
                .FirstOrDefaultAsync(v => v.VisitRequestId == transfer.VisitRequestId, cancellationToken)
                ?? throw new NotFoundException("Đơn đăng ký tham quan", transfer.VisitRequestId);
            if (visit.VisitorUserId != transfer.OldUserId)
                throw new ConflictException(
                    "Đầu mối của đơn đã thay đổi ngoài lời mời này nên lời mời không còn hiệu lực.",
                    VisitContactTransferErrorCodes.Conflict);
            if ((uint)visit.RowVersion != transfer.ExpectedRequestRowVersion)
                throw new ConflictException(
                    "Đơn đã được cập nhật sau khi lời mời được gửi. Vui lòng đề nghị gửi lại lời mời.",
                    VisitContactTransferErrorCodes.Conflict);
            TransferGuards.EnsureTransferLifecycleOpen(visit, now);

            oldOwnerId = visit.VisitorUserId;
            var oldMasked = VisitRequestFingerprintBuilder.MaskEmail(
                VisitRequestFingerprintBuilder.NormalizeEmail(visit.ContactPersonEmail));

            // ── THE swap: relation + snapshot only. Old account stays ACTIVE; nothing campus-side moves. ──
            var snapshot = JsonSerializer.Deserialize<PendingContactSnapshot>(
                    transfer.PendingSnapshotJson ?? "{}",
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new PendingContactSnapshot();
            visit.VisitorUserId = actorId;
            visit.ContactPersonFullName = snapshot.FullName ?? actor.FullName;
            visit.ContactPersonOrganization = snapshot.Organization;
            visit.ContactPersonPhone = snapshot.Phone ?? visit.ContactPersonPhone;
            visit.ContactPersonEmail = transfer.NewEmailNormalized!;
            visit.PrimaryContactAccessStatus = PrimaryContactAccessStatuses.Active; // stays ACTIVE throughout
            visit.PrimaryContactVerifiedAt = now;
            visit.RowVersion += 1;
            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;

            transfer.Status = IdentityChangeStatuses.Applied;
            transfer.AppliedAt = now;
            transfer.NewUserId = actorId;
            transfer.UpdatedAt = now;

            token.UsedAt = now;
            token.UsedAction = EmailIntendedActions.Accept;
            token.ResultStatus = EmailActionResultStatuses.Success;
            token.RecipientUserId = actorId;
            await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _db, EmailActionTargetTypes.VisitRequestIdentityChange, transfer.IdentityChangeId,
                "Lời mời chuyển giao đã được chấp nhận.", now, cancellationToken);

            _db.VisitRequestIdentityChangeEvents.Add(new Domain.Entities.Delegations.VisitRequestIdentityChangeEvent
            {
                IdentityChangeId = transfer.IdentityChangeId,
                VisitRequestId = visit.VisitRequestId,
                EventType = "PRIMARY_CONTACT_TRANSFER_APPLIED",
                FromStatus = IdentityChangeStatuses.Pending,
                ToStatus = IdentityChangeStatuses.Applied,
                ActorUserId = actorId,
                EmailMasked = transfer.NewEmailMasked,
                CorrelationId = correlationId,
                CreatedAt = now,
            });
            var audit = new AuditLog
            {
                ActorUserId = actorId,
                Action = "PRIMARY_CONTACT_TRANSFER_APPLIED",
                EntityType = "VisitRequest",
                EntityId = visit.VisitRequestId,
                VisitRequestId = visit.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                CreatedAt = now,
            };
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "visitor_user_id",
                OldValueText = oldOwnerId?.ToString(),
                NewValueText = actorId.ToString(),
                CreatedAt = now,
            });
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "contact_person_email",
                OldValueText = oldMasked, // masked only — audit never stores the full old/new email
                NewValueText = transfer.NewEmailMasked,
                CreatedAt = now,
            });
            _db.AuditLogs.Add(audit);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            visitRequestId = visit.VisitRequestId;
            requestCode = visit.RequestCode ?? string.Empty;
        }

        // ── Post-commit notifications: the OLD owner (lost the relation, account untouched) and the
        //    registrant (initiator visibility). Best-effort; the swap is already committed. ──
        try
        {
            var recipients = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
            void Add(ulong userId, string title, string message) =>
                recipients.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: userId,
                    Title: title,
                    Message: message,
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestSubmitted,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visitRequestId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    IsActionRequired: false,
                    VisitRequestId: visitRequestId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={visitRequestId}"));
            if (oldOwnerId is not null && oldOwnerId != actorId)
                Add(oldOwnerId.Value, "Vai trò đầu mối liên hệ đã được chuyển giao",
                    $"Vai trò đầu mối liên hệ của đơn {requestCode} đã được chuyển cho người khác. Tài khoản của bạn không bị ảnh hưởng.");
            var registrantId = await _db.VisitRequests.AsNoTracking()
                .Where(v => v.VisitRequestId == visitRequestId)
                .Select(v => v.RegistrantUserId)
                .FirstAsync(cancellationToken);
            if (registrantId is not null && registrantId != actorId && registrantId != oldOwnerId)
                Add(registrantId.Value, "Chuyển giao đầu mối đã hoàn tất",
                    $"Đầu mối liên hệ mới đã xác nhận cho đơn {requestCode}.");
            if (recipients.Count > 0)
                await _notificationService.CreateManyAsync(recipients, cancellationToken);
        }
        catch
        {
            // best-effort — never fail the applied transfer for a notification problem
        }

        return new VisitContactTransferActionResponse(
            visitRequestId, requestCode,
            IdentityChangeStatuses.Applied, PrimaryContactAccessStatuses.Active,
            Idempotent: false,
            "Bạn đã trở thành đầu mối liên hệ của đơn. Đầu mối cũ không còn quyền quản lý đơn này.");
    }

    private sealed class PendingContactSnapshot
    {
        public string? FullName { get; set; }
        public string? Organization { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}

/// <summary>Shared token/state guards for the invited-side transfer actions (accept/decline).</summary>
internal static class TransferTokenGuards
{
    /// <summary>Flag/auth/token-hash resolution (pre-transaction). Returns (transferId, actorId).</summary>
    public static async Task<(ulong TransferId, ulong ActorId)> ResolveAsync(
        PerCampusFormV2WriteOptions writeFlag, ICurrentUserService currentUser,
        IEmailActionTokenService tokens, IApplicationDbContext db, string rawToken, CancellationToken ct)
    {
        if (!writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaim.VisitContactClaimErrorCodes.TokenInvalid);

        var hash = tokens.Hash(rawToken.Trim());
        var transferId = await db.EmailActionTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash
                        && t.ActionContext == EmailActionContexts.VisitContactTransfer
                        && t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange)
            .Select(t => (ulong?)t.TargetId)
            .FirstOrDefaultAsync(ct);
        if (transferId is null)
            throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaim.VisitContactClaimErrorCodes.TokenInvalid);
        return (transferId.Value, currentUser.UserId.Value);
    }

    /// <summary>Maps a settled/overdue transfer to its stable error (called on the LOCKED row).</summary>
    public static void EnsurePendingTransferState(
        Domain.Entities.Delegations.VisitRequestIdentityChange transfer, DateTime now)
    {
        if (transfer.Status == IdentityChangeStatuses.Superseded)
            throw new ConflictException(
                "Lời mời này đã được thay bằng lời mời mới hơn.",
                VisitContactTransferErrorCodes.Superseded);
        if (transfer.Status != IdentityChangeStatuses.Pending)
            throw new ConflictException(
                "Lời mời chuyển giao không còn hiệu lực (đã được xử lý).",
                VisitContactTransferErrorCodes.Conflict);
        if (transfer.ExpiresAt <= now)
            throw new ConflictException(
                "Lời mời chuyển giao đã hết hạn (hiệu lực 24 giờ). Đầu mối hiện tại vẫn giữ nguyên quyền.",
                VisitContactTransferErrorCodes.Expired);
    }

    /// <summary>Loads the tracked token row for THIS raw token and validates it is still live.</summary>
    public static async Task<Domain.Entities.Emails.EmailActionToken> LoadLiveTokenAsync(
        IApplicationDbContext db, IEmailActionTokenService tokens, string rawToken, ulong transferId,
        DateTime now, CancellationToken ct)
    {
        var hash = tokens.Hash(rawToken.Trim());
        var token = await db.EmailActionTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash
                                      && t.ActionContext == EmailActionContexts.VisitContactTransfer
                                      && t.TargetId == transferId, ct)
            ?? throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaim.VisitContactClaimErrorCodes.TokenInvalid);
        if (token.UsedAt is not null || token.ResultStatus != EmailActionResultStatuses.Pending
            || token.ExpiresAt <= now)
            throw new ConflictException(
                "Liên kết đã được sử dụng hoặc đã hết hạn. Vui lòng đề nghị gửi lại lời mời.",
                VisitContactClaim.VisitContactClaimErrorCodes.TokenInvalid);
        return token;
    }
}

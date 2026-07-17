using System;
using System.Linq;
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

namespace PEMS.Application.Delegations.Commands.VisitContactClaim;

/// <summary>
/// The verified REGISTRANT re-sends the pending INITIAL_CLAIM invitation (plan §16.4). Every outstanding
/// token of the claim is invalidated FIRST (a resend supersedes — the old link can never race the new one),
/// the claim expiry window restarts (72h) and a fresh token+email goes out AFTER commit. Capped by
/// <see cref="MaxResends"/> to keep the invitation from becoming a spam channel.
/// </summary>
public sealed class ResendVisitContactClaimCommandHandler
    : IRequestHandler<ResendVisitContactClaimCommand, VisitContactClaimManageResponse>
{
    private const int MaxResends = 5;
    private const int ClaimValidityHours = 72;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitContactClaimService _claimService;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public ResendVisitContactClaimCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitContactClaimService claimService, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _claimService = claimService;
        _writeFlag = writeFlag;
    }

    public async Task<VisitContactClaimManageResponse> Handle(
        ResendVisitContactClaimCommand request, CancellationToken cancellationToken)
    {
        var actorId = RegistrantClaimGuard.EnsureAuthenticatedVisitor(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;

        ulong claimIdToSend;
        await using (var tx = await _db.BeginTransactionAsync(cancellationToken))
        {
            var visit = await RegistrantClaimGuard.LoadRequestForRegistrantAsync(
                _db, request.VisitRequestId, actorId, cancellationToken);

            var claim = await _claimService.LockPendingInitialClaimAsync(
                    visit.VisitRequestId, cancellationToken)
                ?? throw new ConflictException(
                    "Không có lời mời đầu mối nào đang chờ xác nhận để gửi lại.",
                    VisitContactClaimErrorCodes.NoPendingClaim);

            if (claim.ResendCount >= MaxResends)
                throw new BusinessRuleException(
                    "Đã vượt quá số lần gửi lại lời mời cho đơn này.",
                    VisitContactClaimErrorCodes.ResendLimit);

            // Supersede every outstanding link BEFORE the new one exists.
            await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
                _db, EmailActionTargetTypes.VisitRequestIdentityChange, claim.IdentityChangeId,
                "Lời mời đã được gửi lại bằng liên kết mới.", now, cancellationToken);

            claim.ResendCount += 1;
            claim.ExpiresAt = now.AddHours(ClaimValidityHours);
            claim.UpdatedAt = now;

            var correlationId = Guid.NewGuid().ToString("N");
            _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
            {
                IdentityChangeId = claim.IdentityChangeId,
                VisitRequestId = visit.VisitRequestId,
                EventType = "PRIMARY_CONTACT_INVITATION_RESENT",
                FromStatus = IdentityChangeStatuses.Pending,
                ToStatus = IdentityChangeStatuses.Pending,
                ActorUserId = actorId,
                EmailMasked = claim.NewEmailMasked,
                Reason = $"resend_count={claim.ResendCount};new_expiry={claim.ExpiresAt:yyyy-MM-dd HH:mm}",
                CorrelationId = correlationId,
                CreatedAt = now,
            });
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                Action = "PRIMARY_CONTACT_INVITATION_RESENT",
                EntityType = "VisitRequest",
                EntityId = visit.VisitRequestId,
                VisitRequestId = visit.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            claimIdToSend = claim.IdentityChangeId;

            // AFTER commit: mint the fresh token + send the email (best-effort; own SaveChanges).
            await _claimService.SendInvitationAsync(claimIdToSend, cancellationToken);

            return new VisitContactClaimManageResponse(
                visit.VisitRequestId, visit.PrimaryContactAccessStatus,
                IdentityChangeStatuses.Pending, claim.ResendCount,
                "Đã gửi lại lời mời xác nhận tới email đầu mối. Liên kết cũ không còn hiệu lực.");
        }
    }
}

/// <summary>Shared guards for the registrant-side claim management commands (resend / replace).</summary>
internal static class RegistrantClaimGuard
{
    public static ulong EnsureAuthenticatedVisitor(
        PerCampusFormV2WriteOptions writeFlag, ICurrentUserService currentUser)
    {
        if (!writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            throw new ForbiddenException();
        if (currentUser.RoleCode != RoleCodes.Visitor)
            throw new ForbiddenException("Chỉ khách (Visitor) mới được quản lý lời mời đầu mối.");
        return currentUser.UserId.Value;
    }

    /// <summary>
    /// Loads the request (tracked) and enforces the registrant-while-unclaimed policy: actor must be the
    /// REGISTRANT, the request must be per-campus v2, not cancelled, and the primary contact must still be
    /// PENDING_CONFIRMATION with no owner — once the contact is ACTIVE this is a TRANSFER, not a claim edit.
    /// </summary>
    public static async Task<VisitRequest> LoadRequestForRegistrantAsync(
        IApplicationDbContext db, ulong visitRequestId, ulong actorId, CancellationToken ct)
    {
        var visit = await db.VisitRequests
            .FirstOrDefaultAsync(v => v.VisitRequestId == visitRequestId, ct)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", visitRequestId);

        if (visit.RegistrantUserId != actorId)
            throw new ForbiddenException("Chỉ người đăng ký đơn mới được quản lý lời mời đầu mối.");
        if (visit.FormSchemaVersion < FormSchemaVersions.PerCampus)
            throw new BusinessRuleException(
                "Đơn này không dùng biểu mẫu theo cơ sở (v2).",
                VisitFormV2ErrorCodes.FormVersionUpgradeRequired);
        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new BusinessRuleException(
                "Đơn đã bị hủy nên không thể quản lý lời mời đầu mối.",
                VisitContactClaimErrorCodes.NotPending);
        if (visit.PrimaryContactAccessStatus != PrimaryContactAccessStatuses.PendingConfirmation
            || visit.VisitorUserId is not null)
            throw new ConflictException(
                "Đầu mối liên hệ đã xác nhận. Việc đổi đầu mối lúc này phải qua quy trình chuyển giao.",
                VisitContactClaimErrorCodes.NotPending);
        return visit;
    }
}

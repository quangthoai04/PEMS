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
/// Applies the INITIAL_CLAIM (plan §16.4/§4.4): the invited contact, logged in with the Google account whose
/// email matches <c>new_email_normalized</c>, explicitly accepts — ONLY then is the VISITOR linked:
/// <c>visitor_user_id</c> is set and <c>primary_contact_access_status</c> flips to ACTIVE in the SAME
/// transaction as PENDING → APPLIED. Token possession alone NEVER applies a claim (the generic anonymous
/// email-action handler rejects this context). Campus decisions are never touched — a claim after approval
/// does not reset anything. Concurrency: the claim row is locked (<c>SELECT … FOR UPDATE</c>) so a concurrent
/// accept/decline/resend serializes and the loser sees a non-PENDING state.
/// </summary>
public sealed class AcceptVisitContactClaimCommandHandler
    : IRequestHandler<AcceptVisitContactClaimCommand, VisitContactClaimActionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IEmailActionTokenService _tokens;
    private readonly IVisitContactClaimService _claimService;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public AcceptVisitContactClaimCommandHandler(
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
        AcceptVisitContactClaimCommand request, CancellationToken cancellationToken)
    {
        var (claimId, actorId) = await GuardAsync(request.Token, cancellationToken);
        var now = _clock.VietnamNow;

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var (claim, token, visit, actor) = await LoadLockedAsync(claimId, request.Token, actorId, cancellationToken);

        // The authenticated account must BE the invited email (DB row email — the SSO login already proved
        // ownership of it). A different Google account can never accept someone else's invitation.
        var actorEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(actor.Email);
        if (actorEmailNorm != claim.NewEmailNormalized)
            throw new ConflictException(
                "Tài khoản đang đăng nhập không trùng với email được mời. Vui lòng đăng nhập đúng tài khoản Google của email nhận lời mời.",
                VisitContactClaimErrorCodes.EmailMismatch);

        // ── Apply: link + activate + close claim + burn token, atomically ──
        visit.VisitorUserId = actorId;
        visit.PrimaryContactAccessStatus = PrimaryContactAccessStatuses.Active;
        visit.PrimaryContactVerifiedAt = now;
        visit.RowVersion += 1;
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;

        claim.Status = IdentityChangeStatuses.Applied;
        claim.AppliedAt = now;
        claim.NewUserId = actorId;
        claim.UpdatedAt = now;

        token.UsedAt = now;
        token.UsedAction = EmailIntendedActions.Accept;
        token.ResultStatus = EmailActionResultStatuses.Success;
        token.RecipientUserId = actorId;

        // Any other outstanding token of this claim (e.g. from a resend) is now dead.
        await EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.VisitRequestIdentityChange, claim.IdentityChangeId,
            "Lời mời đã được chấp nhận.", now, cancellationToken);

        var correlationId = Guid.NewGuid().ToString("N");
        _db.VisitRequestIdentityChangeEvents.Add(new Domain.Entities.Delegations.VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = claim.IdentityChangeId,
            VisitRequestId = visit.VisitRequestId,
            EventType = "PRIMARY_CONTACT_CLAIM_APPLIED",
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Applied,
            ActorUserId = actorId,
            EmailMasked = claim.NewEmailMasked,
            CorrelationId = correlationId,
            CreatedAt = now,
        });

        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "PRIMARY_CONTACT_CLAIM_APPLIED",
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
            OldValueText = null,
            NewValueText = actorId.ToString(),
            CreatedAt = now,
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "primary_contact_access_status",
            OldValueText = PrimaryContactAccessStatuses.PendingConfirmation,
            NewValueText = PrimaryContactAccessStatuses.Active,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(audit);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new VisitContactClaimActionResponse(
            visit.VisitRequestId, visit.RequestCode ?? string.Empty,
            IdentityChangeStatuses.Applied, PrimaryContactAccessStatuses.Active,
            "Bạn đã trở thành đầu mối liên hệ của đơn đăng ký. Bây giờ bạn có thể theo dõi và quản lý đơn.");
    }

    // ── Shared pre-transaction guard (flags/auth) — returns claim id + actor id. ──
    private async Task<(ulong ClaimId, ulong ActorId)> GuardAsync(string rawToken, CancellationToken ct)
    {
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaimErrorCodes.TokenInvalid);

        var hash = _tokens.Hash(rawToken.Trim());
        var claimId = await _db.EmailActionTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash
                        && t.ActionContext == EmailActionContexts.VisitContactClaim
                        && t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange)
            .Select(t => (ulong?)t.TargetId)
            .FirstOrDefaultAsync(ct);
        if (claimId is null)
            throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaimErrorCodes.TokenInvalid);
        return (claimId.Value, _currentUser.UserId.Value);
    }

    /// <summary>
    /// Inside the transaction: locks the claim (FOR UPDATE via <see cref="IVisitContactClaimService"/> —
    /// the Application layer has no relational EF dependency), re-reads the token, request and actor, and
    /// re-validates the live state. Shared by accept and decline.
    /// </summary>
    internal static async Task<(
        Domain.Entities.Delegations.VisitRequestIdentityChange Claim,
        Domain.Entities.Emails.EmailActionToken Token,
        Domain.Entities.Delegations.VisitRequest Visit,
        User Actor)> LoadLockedForClaimActionAsync(
        IApplicationDbContext db, IEmailActionTokenService tokens, IDateTimeService clock,
        IVisitContactClaimService claimService,
        ulong claimId, string rawToken, ulong actorId, CancellationToken ct)
    {
        var now = clock.VietnamNow;

        var claim = await claimService.LockClaimAsync(claimId, ct)
            ?? throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaimErrorCodes.TokenInvalid);

        if (claim.Status != IdentityChangeStatuses.Pending || claim.ExpiresAt <= now)
            throw new ConflictException(
                "Lời mời không còn hiệu lực (đã được xử lý hoặc đã hết hạn).",
                VisitContactClaimErrorCodes.NotPending);

        var hash = tokens.Hash(rawToken.Trim());
        var token = await db.EmailActionTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash
                                      && t.ActionContext == EmailActionContexts.VisitContactClaim
                                      && t.TargetId == claimId, ct)
            ?? throw new ConflictException("Liên kết không hợp lệ.", VisitContactClaimErrorCodes.TokenInvalid);
        if (token.UsedAt is not null || token.ResultStatus != EmailActionResultStatuses.Pending
            || token.ExpiresAt <= now)
            throw new ConflictException(
                "Liên kết đã được sử dụng hoặc đã hết hạn. Vui lòng đề nghị người đăng ký gửi lại lời mời.",
                VisitContactClaimErrorCodes.TokenInvalid);

        var visit = await db.VisitRequests
            .FirstOrDefaultAsync(v => v.VisitRequestId == claim.VisitRequestId, ct)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", claim.VisitRequestId);
        if (visit.FormSchemaVersion < FormSchemaVersions.PerCampus
            || visit.Status == VisitRequestStatuses.Cancelled
            || visit.VisitorUserId is not null)
            throw new ConflictException(
                "Lời mời không còn hiệu lực cho đơn này.",
                VisitContactClaimErrorCodes.NotPending);

        var actor = await db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == actorId, ct)
            ?? throw new ForbiddenException();
        if (actor.Role?.RoleCode != RoleCodes.Visitor)
            throw new ConflictException(
                "Tài khoản nội bộ không thể nhận vai trò đầu mối liên hệ khách.",
                VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount);
        if (actor.Status != UserStatuses.Active)
            throw new BusinessRuleException(
                "Tài khoản khách này đang không hoạt động.",
                VisitRequestErrorCodes.VisitorAccountInactive);

        return (claim, token, visit, actor);
    }

    private Task<(
        Domain.Entities.Delegations.VisitRequestIdentityChange Claim,
        Domain.Entities.Emails.EmailActionToken Token,
        Domain.Entities.Delegations.VisitRequest Visit,
        User Actor)> LoadLockedAsync(ulong claimId, string rawToken, ulong actorId, CancellationToken ct)
        => LoadLockedForClaimActionAsync(_db, _tokens, _clock, _claimService, claimId, rawToken, actorId, ct);
}

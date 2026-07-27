using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.ConfirmAccountEmail;

/// <summary>
/// Confirms email ownership for a <c>PENDING_EMAIL_CONFIRMATION</c> account and activates it EXACTLY once.
/// The token is matched by hash; it must be PENDING, unexpired, and its target email must still equal the
/// account's current email (so a stale token after an email edit is rejected). Confirming activates the
/// user, marks the token CONFIRMED, supersedes any other live token, and audits — all in one transaction.
/// A replay of an already-confirmed token returns a stable ALREADY_CONFIRMED without re-activating.
/// Failures are deliberately identity-free to avoid token/account enumeration.
/// </summary>
public sealed class ConfirmAccountEmailCommandHandler
    : IRequestHandler<ConfirmAccountEmailCommand, ConfirmAccountEmailResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailActionTokenService _tokens;
    private readonly IDateTimeService _clock;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IAccountEmailConfirmationService _confirmations;

    public ConfirmAccountEmailCommandHandler(
        IApplicationDbContext db, IEmailActionTokenService tokens, IDateTimeService clock,
        ISystemEmailDispatcher dispatcher, IAccountEmailConfirmationService confirmations)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _dispatcher = dispatcher;
        _confirmations = confirmations;
    }

    public async Task<ConfirmAccountEmailResponse> Handle(
        ConfirmAccountEmailCommand request, CancellationToken cancellationToken)
    {
        var token = (request.Token ?? string.Empty).Trim();
        if (token.Length == 0) return Invalid();

        var hash = _tokens.Hash(token);
        var confirmation = await _db.AccountEmailConfirmations
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.TokenHash == hash, cancellationToken);

        if (confirmation is null) return Invalid();

        // Idempotent replay: an already-confirmed token never activates a second time.
        if (confirmation.Status == AccountEmailConfirmationStatuses.Confirmed)
            return new ConfirmAccountEmailResponse
            {
                Success = true,
                Status = ConfirmAccountEmailStatuses.AlreadyConfirmed,
                Message = "Email đã được xác nhận trước đó. Bạn có thể đăng nhập.",
            };

        // Superseded / cancelled / already-expired-marked → no longer usable.
        if (confirmation.Status != AccountEmailConfirmationStatuses.Pending) return Invalid();

        var now = _clock.VietnamNow;
        if (confirmation.ExpiresAt < now)
        {
            confirmation.Status = AccountEmailConfirmationStatuses.Expired;
            confirmation.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new ConfirmAccountEmailResponse
            {
                Success = false,
                Status = ConfirmAccountEmailStatuses.Expired,
                Message = "Liên kết xác nhận đã hết hạn. Vui lòng yêu cầu gửi lại email xác nhận.",
            };
        }

        var user = confirmation.User;
        if (user is null) return Invalid();

        // The token's target email must still equal the account's current email (a stale token issued for a
        // now-changed email must never activate).
        if (!string.Equals(confirmation.TargetEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            return Invalid();

        // Only a pending account activates; any other status (already active, inactive, locked) never does.
        if (user.Status != UserStatuses.PendingEmailConfirmation) return Invalid();

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            user.Status = UserStatuses.Active;
            confirmation.Status = AccountEmailConfirmationStatuses.Confirmed;
            confirmation.ConfirmedAt = now;
            confirmation.UpdatedAt = now;

            // Revoke any OTHER live token for this user (defence in depth — normally there is none).
            var others = await _db.AccountEmailConfirmations
                .Where(c => c.UserId == user.UserId
                            && c.ConfirmationId != confirmation.ConfirmationId
                            && c.Status == AccountEmailConfirmationStatuses.Pending)
                .ToListAsync(cancellationToken);
            foreach (var other in others)
            {
                other.Status = AccountEmailConfirmationStatuses.Superseded;
                other.UpdatedAt = now;
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = user.UserId,
                CampusId = user.PrimaryCampusId,
                Action = "CONFIRM_ACCOUNT_EMAIL",
                EntityType = "User",
                EntityId = user.UserId,
                Changes = new List<AuditLogChange>(),
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Welcome email — ONLY after confirmation, best-effort (a non-Sent outcome is fine; the account is
        // already active). Never blocks or fails the confirmation.
        try
        {
            // The role is read by id: the account row is tracked here but its Role navigation is not
            // necessarily loaded, and the email states the role the owner now holds.
            var roleCode = await _db.Roles.AsNoTracking()
                .Where(r => r.RoleId == user.RoleId)
                .Select(r => r.RoleCode)
                .FirstOrDefaultAsync(cancellationToken) ?? RoleCodes.Staff;

            await _dispatcher.SendAsync(new SystemEmailRequest(
                SystemEmailTemplates.AccountActivated,
                new EmailRecipient(user.Email, user.FullName),
                await AccountEmailVariables.ForActivatedAsync(
                    _db, user, roleCode, user.SubRole, cancellationToken),
                TrustedBlocks: AccountEmailVariables.LoginBlocks(_confirmations.BuildLoginUrl()),
                RelatedType: "User",
                RelatedId: user.UserId), cancellationToken);
        }
        catch { /* welcome is best-effort */ }

        return new ConfirmAccountEmailResponse
        {
            Success = true,
            Status = ConfirmAccountEmailStatuses.Confirmed,
            Message = "Xác nhận email thành công. Tài khoản đã được kích hoạt.",
        };
    }

    private static ConfirmAccountEmailResponse Invalid() => new()
    {
        Success = false,
        Status = ConfirmAccountEmailStatuses.Invalid,
        Message = "Liên kết xác nhận không hợp lệ hoặc đã được sử dụng.",
    };
}

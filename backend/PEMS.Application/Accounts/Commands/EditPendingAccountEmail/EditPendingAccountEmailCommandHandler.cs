using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.EditPendingAccountEmail;

/// <summary>
/// Changes a pending account's email (and, optionally, its full name in the same breath). Authorized to
/// HO / the account's Staff Leader. The new email is normalized, validated and checked for uniqueness;
/// the address is updated, the authentication bindings are re-pointed, and a fresh confirmation token is
/// issued — which supersedes the old one, so the link mailed to the previous address stops working.
///
/// <para>
/// The mail sent to the NEW address is the SAME <c>ACCOUNT_EMAIL_CONFIRMATION</c> template the create
/// flow uses, complete with the activation button: the account has still never proven it owns an
/// address, so what the new holder needs is a way to activate, not a notice that something changed.
/// A neutral notice goes to the old address. The account stays PENDING until the holder confirms —
/// nothing here activates it.
/// </para>
/// </summary>
public sealed class EditPendingAccountEmailCommandHandler
    : IRequestHandler<EditPendingAccountEmailCommand, EditPendingAccountEmailResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IAccountEmailConfirmationService _confirmations;
    private readonly ISystemEmailDispatcher _dispatcher;

    public EditPendingAccountEmailCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IAccountEmailConfirmationService confirmations, ISystemEmailDispatcher dispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _confirmations = confirmations;
        _dispatcher = dispatcher;
    }

    public async Task<EditPendingAccountEmailResponse> Handle(
        EditPendingAccountEmailCommand request, CancellationToken cancellationToken)
    {
        // Role for the confirmation email's variables, AuthProviders because the address change has to
        // re-point them — both are loaded up front so the write below needs no second round trip.
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);
        if (user is null) throw new NotFoundException("Tài khoản không tồn tại.");

        PendingAccountAuthorization.EnsureCanManagePending(_currentUser, user);

        if (user.Status != UserStatuses.PendingEmailConfirmation)
            throw new BusinessRuleException(
                "Chỉ có thể sửa email của tài khoản đang chờ xác nhận.", "ACCOUNT_NOT_PENDING");

        // Full name is optional; when sent it is held to the same shared rules as every other HO flow,
        // so a direct API call cannot store a name the modal would have rejected.
        var oldFullName = user.FullName;
        var newFullName = oldFullName;
        if (request.FullName is not null)
        {
            newFullName = AccountIdentityRules.NormalizeFullName(request.FullName);
            if (AccountIdentityRules.ValidateFullName(newFullName) is { } fullNameError)
                throw new ValidationException(fullNameError);
        }

        var newEmail = AccountIdentityRules.NormalizeEmail(request.NewEmail);
        if (AccountIdentityRules.ValidateEmail(newEmail) is { } emailError)
            throw new ValidationException(emailError);

        var oldEmail = user.Email;
        if (string.Equals(newEmail, oldEmail, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Email mới trùng với email hiện tại.", "EMAIL_UNCHANGED");

        if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == newEmail && u.UserId != user.UserId, cancellationToken))
            throw new ConflictException(
                AccountIdentityRules.EmailAlreadyUsedMessage, AccountErrorCodes.EmailAlreadyExists);

        var now = _clock.VietnamNow;
        var actorId = _currentUser.UserId;

        // The confirmation email states the account's role. Prefer the loaded navigation, falling back
        // to a lookup by id so a context that did not materialize it still produces a correct email.
        var roleCode = user.Role?.RoleCode
            ?? await _db.Roles.AsNoTracking()
                .Where(r => r.RoleId == user.RoleId)
                .Select(r => r.RoleCode)
                .FirstOrDefaultAsync(cancellationToken)
            ?? RoleCodes.Staff;

        // ── Everything that must be true together: the new identity, the re-pointed providers, the
        //    dead old token and the live new one. A half-applied version of this leaves an account
        //    whose only live link points at an address it no longer has.
        string rawToken;
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            user.FullName = newFullName;
            user.Email = newEmail;

            // Unlink SSO/FEID by DELETING the row: provider_subject identifies the OLD external
            // identity and a subject-less SSO/FEID row is rejected by the database outright. Removing
            // it is what "re-link on next login" means — the login handler creates a fresh row when the
            // user has none. Same policy as the basic-info flow, so an account cannot end up bound to
            // an external identity that was proven against a different address.
            var externalProviders = user.AuthProviders
                .Where(p => p.ProviderType == ProviderTypes.GoogleSso || p.ProviderType == ProviderTypes.FeId)
                .ToList();
            _db.UserAuthProviders.RemoveRange(externalProviders);

            // Local-password logins stay linked — only the address they point at changes.
            foreach (var provider in user.AuthProviders)
            {
                if (provider.ProviderType == ProviderTypes.GoogleSso || provider.ProviderType == ProviderTypes.FeId)
                    continue;
                provider.ProviderEmail = newEmail;
            }

            // A pending account has verified nothing; after the address moves that is doubly true.
            user.EmailVerifiedAt = null;
            user.UpdatedAt = now;
            user.UpdatedBy = actorId;

            // Fresh token bound to the NEW email. Supersedes every live token for this user, so the
            // link already mailed to the old address stops confirming. isResend:false — this is a
            // corrected address starting its own series, not another attempt at the same one.
            rawToken = await _confirmations.IssuePendingAsync(user.UserId, newEmail, isResend: false, cancellationToken);

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = user.PrimaryCampusId ?? _currentUser.PrimaryCampusId,
                Action = "EDIT_PENDING_ACCOUNT_EMAIL",
                EntityType = "User",
                EntityId = user.UserId,
                Changes = new List<AuditLogChange>
                {
                    // Addresses and names only. The raw token and the confirmation URL are deliberately
                    // absent: an audit row is read by more people than may activate the account.
                    new AuditLogChange
                    {
                        FieldName = "PendingAccountEmail",
                        OldValueText = JsonSerializer.Serialize(new
                        {
                            email = oldEmail,
                            fullName = oldFullName,
                        }),
                        NewValueText = JsonSerializer.Serialize(new
                        {
                            email = newEmail,
                            fullName = newFullName,
                            oldConfirmationSuperseded = true,
                            newConfirmationIssued = true,
                        }),
                    },
                },
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

        // ── Post-commit. A delivery failure must not undo a committed address change: the account is
        //    correct and still pending either way, and HO can fall back on "Gửi lại email xác nhận".
        var notificationStatus = "FAILED";
        try
        {
            var result = await _dispatcher.SendAsync(new SystemEmailRequest(
                SystemEmailTemplates.AccountEmailConfirmation,
                new EmailRecipient(newEmail, user.FullName),
                await AccountEmailVariables.ForConfirmationAsync(
                    _db, user.FullName, roleCode, user.SubRole,
                    user.PrimaryCampusId, _confirmations.ExpiryHours, cancellationToken),
                TrustedBlocks: AccountEmailVariables.ConfirmationBlocks(_confirmations.BuildConfirmUrl(rawToken)),
                RelatedType: "User",
                RelatedId: user.UserId,
                SentBy: _currentUser.UserId), cancellationToken);

            notificationStatus = result.NotificationStatus;
        }
        catch
        {
            // Reported as FAILED rather than thrown: the caller needs to be told the address WAS
            // changed, which a 500 would hide.
        }

        // Neutral notice to the previous address — best-effort, never blocks, and never affects the
        // status above: the mail that decides whether this account can be activated is the one sent to
        // the NEW address.
        //
        // Deliberately anonymous: the address being unlinked may have been a typo and belong to somebody
        // with no connection to this account. No variables, and no display name on the recipient either —
        // a To header reading "Nguyễn Văn A <stranger@…>" would name the holder just as effectively as
        // the body would.
        try
        {
            await _dispatcher.SendAsync(new SystemEmailRequest(
                SystemEmailTemplates.AccountPendingEmailChangedOldNotice,
                new EmailRecipient(oldEmail),
                new Dictionary<string, string>(),
                RelatedType: "User",
                RelatedId: user.UserId,
                SentBy: _currentUser.UserId), cancellationToken);
        }
        catch { /* notice is best-effort */ }

        return new EditPendingAccountEmailResponse
        {
            Success = true,
            Email = newEmail,
            EmailNotificationStatus = notificationStatus,
            Message = "Đã cập nhật email và gửi lại xác nhận.",
        };
    }
}

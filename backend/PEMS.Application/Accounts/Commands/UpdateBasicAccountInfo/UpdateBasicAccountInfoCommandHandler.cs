using System.Collections.Generic;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.UpdateBasicAccountInfo;

/// <summary>
/// HO basic-info edit handler (spec §9/§10). Updates ONLY full name + login email of another HO or
/// a Staff Leader. Role/sub-role/campus/department/status are loaded from the database and never
/// changed. When the email changes: local-password providers are re-pointed at the new address, the
/// SSO/FEID provider rows are removed so the account re-links on next login, email verification is
/// cleared, and every active session is revoked so the old email can no longer sign in. This is the final authorization gate —
/// a direct API call from a non-HO caller (or on an out-of-scope / self / LOCKED target) is rejected.
/// </summary>
public sealed class UpdateBasicAccountInfoCommandHandler
    : IRequestHandler<UpdateBasicAccountInfoCommand, UpdateBasicAccountInfoResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;
    private readonly ISessionService _sessionService;
    private readonly IDateTimeService _clock;
    private readonly ISystemEmailDispatcher _dispatcher;

    public UpdateBasicAccountInfoCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy,
        ISessionService sessionService,
        IDateTimeService clock,
        ISystemEmailDispatcher dispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _sessionService = sessionService;
        _clock = clock;
        _dispatcher = dispatcher;
    }

    public async Task<UpdateBasicAccountInfoResponse> Handle(
        UpdateBasicAccountInfoCommand request, CancellationToken cancellationToken)
    {
        var actorId = _currentUser.UserId;

        // 1. Caller authenticated. 2/3. Caller is HO with account-management permission.
        if (!_currentUser.IsAuthenticated || actorId is null)
            throw new AuthBusinessException(AccountErrorCodes.AccountListForbidden,
                "Bạn cần đăng nhập để thực hiện thao tác này.", 401);

        if (_currentUser.RoleCode != RoleCodes.Ho || !_accessPolicy.CanAccessAccountManagement(_currentUser))
            throw new ForbiddenException("Chỉ Head Office mới được chỉnh sửa thông tin tài khoản này.");

        // 4. Target exists (role loaded from DB — never trusted from the client). Campus and department
        // used to be loaded here for the notification email; the change notice no longer restates the
        // account snapshot, so nothing needs them.
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Account", request.UserId);

        // 5. Not the caller. 6. Not LOCKED. 7. In HO scope (HO or STAFF/LEADER).
        if (user.UserId == actorId.Value)
            throw new ForbiddenException("Bạn không thể chỉnh sửa thông tin tài khoản của chính mình.");

        if (user.Status == UserStatuses.Locked)
            throw new BusinessRuleException(
                "Tài khoản đang bị khóa vì lý do bảo mật và không thể chỉnh sửa tại đây.");

        var targetInScope = user.Role.RoleCode == RoleCodes.Ho
            || (user.Role.RoleCode == RoleCodes.Staff && user.SubRole == UserSubRoles.Leader);
        if (!targetInScope)
            throw new ForbiddenException("Tài khoản này nằm ngoài phạm vi quản lý của HO.");

        // 9. Only full name / email are accepted (the command carries nothing else). Both go through
        //    the shared identity rules on the NORMALIZED value, so a direct API call is held to the
        //    exact same standard as the modal.
        var newFullName = AccountIdentityRules.NormalizeFullName(request.FullName);
        if (AccountIdentityRules.ValidateFullName(newFullName) is { } nameError)
            throw new ValidationException(nameError);

        var newEmail = AccountIdentityRules.NormalizeEmail(request.Email);
        if (AccountIdentityRules.ValidateEmail(newEmail) is { } emailError)
            throw new ValidationException(emailError);

        var oldFullName = user.FullName;
        var oldEmail = user.Email;
        var emailChanged = !string.Equals(oldEmail, newEmail, StringComparison.Ordinal);

        // Email uniqueness (excludes the target itself so re-saving the same email is a no-op).
        if (emailChanged)
        {
            var emailTaken = await _db.Users.AnyAsync(
                u => u.Email == newEmail && u.UserId != user.UserId, cancellationToken);
            if (emailTaken)
                throw new ConflictException(
                    AccountIdentityRules.EmailAlreadyUsedMessage, AccountErrorCodes.EmailAlreadyExists);
        }

        var now = _clock.VietnamNow;
        user.FullName = newFullName;

        if (emailChanged)
        {
            user.Email = newEmail;

            // Unlink SSO/FEID by DELETING the row, not by blanking provider_subject in place: the
            // subject identifies the OLD external identity, and the database rejects a subject-less
            // SSO/FEID row outright (trigger trg_auth_providers_validate_bu — "SSO/FEID
            // provider_subject is required"). Removing the row is also what "re-link on next login"
            // actually means: LoginviaSSOCommandHandler.EnsureGoogleProviderLinkedAsync creates a
            // fresh row when the user has none, and the delete frees the old subject from
            // uq_auth_provider_subject. Sessions referencing the row survive via
            // fk_sessions_auth_provider ON DELETE SET NULL, and are revoked below regardless.
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

            user.EmailVerifiedAt = null;
        }

        user.UpdatedAt = now;
        user.UpdatedBy = actorId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = user.PrimaryCampusId ?? _currentUser.PrimaryCampusId,
            Action = "UPDATE_ACCOUNT_BASIC_INFO",
            EntityType = "User",
            EntityId = user.UserId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "BasicInfo",
                    OldValueText = JsonSerializer.Serialize(new { fullName = oldFullName, email = oldEmail }),
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        fullName = newFullName,
                        email = newEmail,
                        authenticationRelinkRequired = emailChanged,
                    }),
                }
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        // Email change forces every active session out (the old email can no longer sign in).
        var revoked = 0;
        if (emailChanged)
        {
            revoked = await _sessionService.RevokeAllActiveSessionsAsync(
                user.UserId, SessionRevokeReasons.AccountEmailChanged, actorId, cancellationToken);
        }

        // Notifications/emails run after commit; a failure must not roll back the saved account.
        var emailStatus = "NOT_REQUIRED";
        if (emailChanged)
            emailStatus = await SendEmailChangeMailsAsync(user, oldEmail, newEmail, cancellationToken);

        return new UpdateBasicAccountInfoResponse
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            EmailChanged = emailChanged,
            RevokedSessions = revoked,
            EmailNotificationStatus = emailStatus,
            Message = emailChanged
                ? "Cập nhật thông tin tài khoản thành công. Email đăng nhập đã thay đổi và các phiên hiện tại đã bị thu hồi."
                : "Cập nhật thông tin tài khoản thành công.",
        };
    }

    /// <summary>
    /// Sends a removal notice to the OLD email and the change notice to the NEW email.
    /// Returns SENT (both ok), PARTIAL (only one ok) or FAILED (both failed) — the same three values
    /// this endpoint has always returned. Never throws: the two sends are independently guarded so a
    /// failure on the first still attempts the second, and neither can roll back the saved account.
    ///
    /// <para>
    /// The notice to the OLD address stays deliberately anonymous. That address may have been a typo
    /// belonging to an uninvolved person, so it carries no variables at all — no holder name, no new
    /// address, no role/campus — and no display name on the envelope either.
    /// </para>
    /// </summary>
    private async Task<string> SendEmailChangeMailsAsync(
        User user, string oldEmail, string newEmail, CancellationToken cancellationToken)
    {
        var oldOk = await TrySendAsync(new SystemEmailRequest(
            SystemEmailTemplates.AccountEmailChangedOldNotice,
            new EmailRecipient(oldEmail),
            new Dictionary<string, string>(),
            RelatedType: "User",
            RelatedId: user.UserId,
            SentBy: _currentUser.UserId), cancellationToken);

        var newOk = await TrySendAsync(new SystemEmailRequest(
            SystemEmailTemplates.AccountEmailChangedNewNotice,
            new EmailRecipient(newEmail, user.FullName),
            new Dictionary<string, string>
            {
                ["fullName"] = user.FullName,
                // Masked, not in full: the holder is entitled to know WHICH address was unlinked, and
                // seeing part of it is enough to recognise it.
                ["oldEmailMasked"] = AccountEmailVariables.MaskEmail(oldEmail),
            },
            RelatedType: "User",
            RelatedId: user.UserId,
            SentBy: _currentUser.UserId), cancellationToken);

        return (oldOk, newOk) switch
        {
            (true, true) => "SENT",
            (false, false) => "FAILED",
            _ => "PARTIAL",
        };
    }

    /// <summary>
    /// True unless the message definitely did not go out. A SKIPPED send (SMTP disabled outside
    /// production) counts as ok exactly as it did before this call site moved onto the dispatcher —
    /// this endpoint's response contract has only SENT / PARTIAL / FAILED to say it with.
    /// </summary>
    private async Task<bool> TrySendAsync(SystemEmailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dispatcher.SendAsync(request, cancellationToken);
            return result.Delivery.Status != EmailDeliveryStatus.Failed;
        }
        catch
        {
            return false; // non-fatal: the account is already saved and must not be rolled back
        }
    }
}

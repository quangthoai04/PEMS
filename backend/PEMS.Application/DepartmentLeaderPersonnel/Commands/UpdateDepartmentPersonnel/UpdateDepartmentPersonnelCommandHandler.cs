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
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.UpdateDepartmentPersonnel;

/// <summary>
/// Spec §12 — the profile + login-identity edit for department personnel.
///
/// The rule that shapes everything here: <b>the email is editable in every account status, and
/// changing it never changes the status</b> (spec §12.1). PENDING stays PENDING, ACTIVE stays ACTIVE,
/// INACTIVE stays INACTIVE, LOCKED stays LOCKED. An email edit is an identity correction — it is not a
/// back door to activate, unlock or deactivate an account, and it never touches role, sub-role,
/// department or campus.
///
/// What the new address costs depends on the status, and it is handled in exactly two ways:
///
///  • <b>PENDING</b> — the account has never proven ownership of any address. The old confirmation
///    token is superseded and a fresh one is issued BOUND TO THE NEW ADDRESS, so the previous link
///    dies and cannot confirm an address the account no longer has (spec §12.6).
///  • <b>ACTIVE / INACTIVE / LOCKED</b> — the account has (or had) a working identity. SSO/FEID rows
///    are DELETED so the next login re-links against the new address, local-password rows keep their
///    hash and are simply re-pointed, <c>email_verified_at</c> is cleared, and every active session is
///    revoked so the old address cannot keep a live token (spec §12.7/§12.8/§12.9).
///
/// LOCKED additionally keeps <c>failed_login_count</c> and <c>locked_until</c> untouched: fixing a
/// typo in the address must not quietly clear a security lock (spec §12.9).
/// </summary>
public sealed class UpdateDepartmentPersonnelCommandHandler
    : IRequestHandler<UpdateDepartmentPersonnelCommand, UpdateDepartmentPersonnelResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;
    private readonly IUserMutationLockService _lockService;
    private readonly IAccountEmailConfirmationService _confirmations;
    private readonly ISessionService _sessionService;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IDateTimeService _clock;

    public UpdateDepartmentPersonnelCommandHandler(
        IApplicationDbContext db,
        IDepartmentLeaderPersonnelScopeService scopeService,
        IUserMutationLockService lockService,
        IAccountEmailConfirmationService confirmations,
        ISessionService sessionService,
        ISystemEmailDispatcher dispatcher,
        IDateTimeService clock)
    {
        _db = db;
        _scopeService = scopeService;
        _lockService = lockService;
        _confirmations = confirmations;
        _sessionService = sessionService;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    public async Task<UpdateDepartmentPersonnelResponse> Handle(
        UpdateDepartmentPersonnelCommand request, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        // Normalize before comparing: " Nguyen  Van B " and "Nguyen Van B" are the same name, and
        // "A@FPT.EDU.VN" is the same address as "a@fpt.edu.vn". Comparing raw input would report a
        // no-op edit as a change and needlessly revoke every session (spec §12.4).
        var newFullName = AccountIdentityRules.NormalizeFullName(request.FullName);
        if (AccountIdentityRules.ValidateFullName(newFullName) is { } nameError)
            throw new ValidationException(nameError);

        var newEmail = AccountIdentityRules.NormalizeEmail(request.Email);
        if (AccountIdentityRules.ValidateEmail(newEmail) is { } emailError)
            throw new ValidationException(emailError);

        var newPhone = DepartmentPersonnelPhoneRules.Normalize(request.Phone);
        if (DepartmentPersonnelPhoneRules.Validate(newPhone) is { } phoneError)
            throw new ValidationException(phoneError);

        var newGender = DepartmentPersonnelGenders.Parse(request.Gender);

        var now = _clock.VietnamNow;

        string oldEmail;
        string accountStatus;
        bool emailChanged;
        bool anyChange;
        bool confirmationReissued = false;
        bool relinkRequired = false;
        string? rawConfirmationToken = null;
        string targetFullName;
        string? targetSubRole = null;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            // ── Lock FIRST, read after. The target and the department are locked in the same
            //    ascending-id order every other flow uses, so an identity edit and a concurrent
            //    leadership transfer / role change serialize instead of interleaving (spec §12.3). ──
            await _lockService.LockUsersAsync(new[] { request.UserId }, cancellationToken);
            await _lockService.LockDepartmentsAsync(new[] { scope.DepartmentId }, cancellationToken);

            // Re-assert membership under the lock: the row may have moved departments since the gate.
            await _scopeService.EnsureTargetBelongsToCurrentDepartmentAsync(
                scope, request.UserId, cancellationToken);

            var user = await _db.Users
                .Include(u => u.AuthProviders)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
                ?? throw new AuthBusinessException(
                    DepartmentLeaderErrorCodes.PersonnelNotFound,
                    "Không tìm thấy nhân sự trong phòng ban của bạn.", 404);

            oldEmail = user.Email;
            accountStatus = user.Status;
            targetFullName = newFullName;
            // Captured under the lock: the confirmation mail names the account's role, and a Leader
            // being edited must not be described as a Staff member.
            targetSubRole = user.SubRole;

            var oldFullName = user.FullName;
            var oldPhone = user.Phone;
            var oldGender = user.Gender;

            emailChanged = !string.Equals(oldEmail, newEmail, StringComparison.Ordinal);
            var profileChanged = !string.Equals(oldFullName, newFullName, StringComparison.Ordinal)
                                 || !string.Equals(oldPhone ?? string.Empty, newPhone, StringComparison.Ordinal)
                                 || oldGender != newGender;
            anyChange = emailChanged || profileChanged;

            // ── True no-op. No audit row, no updated_at bump, no session revoke, no email. Saying
            //    "updated" here would be a lie and would log a change that never happened. ──
            if (!anyChange)
            {
                await transaction.CommitAsync(cancellationToken);
                return new UpdateDepartmentPersonnelResponse
                {
                    Success = true,
                    UserId = user.UserId,
                    FullName = oldFullName,
                    Email = oldEmail,
                    Phone = oldPhone,
                    Gender = DepartmentPersonnelGenders.ToWire(oldGender),
                    Status = accountStatus,
                    Changed = false,
                    EmailChanged = false,
                    ConfirmationReissued = false,
                    AuthenticationRelinkRequired = false,
                    RevokedSessions = 0,
                    EmailNotificationStatus = DepartmentPersonnelEmails.StatusNotRequired,
                    Message = "Không có thông tin nào thay đổi.",
                };
            }

            if (emailChanged)
            {
                await EnsureEmailIsFreeAsync(newEmail, user.UserId, cancellationToken);
            }

            // ── Profile fields. Always safe — they carry no authentication meaning. ──
            user.FullName = newFullName;
            user.Phone = newPhone;
            user.Gender = newGender;

            if (emailChanged)
            {
                user.Email = newEmail;

                if (accountStatus == UserStatuses.PendingEmailConfirmation)
                {
                    // Never confirmed anything yet: just re-target the proof. IssuePendingAsync
                    // supersedes the live token and, with isResend:false, restarts resend_count for
                    // the new address (spec §12.6).
                    rawConfirmationToken = await _confirmations.IssuePendingAsync(
                        user.UserId, newEmail, isResend: false, cancellationToken);
                    confirmationReissued = true;
                }
                else
                {
                    // A provisioned account (ACTIVE / INACTIVE / LOCKED) — reset the identity bindings.
                    relinkRequired = ResetExternalIdentity(user, newEmail);
                    user.EmailVerifiedAt = null;

                    // Defensive: a non-PENDING account should not own a live confirmation token, but if
                    // one leaked in it must not remain usable against the old address.
                    await SupersedeStrayPendingConfirmationsAsync(user.UserId, now, cancellationToken);
                }
            }

            user.UpdatedAt = now;
            user.UpdatedBy = scope.ActorUserId;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = scope.ActorUserId,
                CampusId = scope.CampusId,
                // Identity edits are a distinct, security-relevant action from a plain profile edit.
                Action = ResolveAuditAction(emailChanged, accountStatus),
                EntityType = "User",
                EntityId = user.UserId,
                Changes = new List<AuditLogChange>
                {
                    new AuditLogChange
                    {
                        FieldName = "PersonnelIdentity",
                        OldValueText = JsonSerializer.Serialize(new
                        {
                            fullName = oldFullName,
                            email = DepartmentPersonnelAudit.MaskEmail(oldEmail),
                            phone = oldPhone,
                            gender = DepartmentPersonnelGenders.ToWire(oldGender),
                            status = accountStatus,
                        }),
                        NewValueText = JsonSerializer.Serialize(new
                        {
                            fullName = newFullName,
                            email = DepartmentPersonnelAudit.MaskEmail(newEmail),
                            phone = newPhone,
                            gender = DepartmentPersonnelGenders.ToWire(newGender),
                            // Unchanged by construction — recorded so the trail proves it.
                            status = accountStatus,
                            emailChanged,
                            confirmationReissued,
                            authenticationRelinkRequired = relinkRequired,
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

        // ── Post-commit side effects. A failure here must never undo a committed identity change. ──
        var revokedSessions = 0;
        if (emailChanged)
        {
            // Every status, including PENDING (which should have none) — the old address must not keep
            // a live token under any circumstance (spec §12.11). A Leader editing their OWN address is
            // signed out too; that is the point, not an edge case to exempt.
            revokedSessions = await _sessionService.RevokeAllActiveSessionsAsync(
                request.UserId,
                SessionRevokeReasons.AccountEmailChanged,
                scope.ActorUserId,
                cancellationToken);
        }

        var emailStatus = emailChanged
            ? await SendIdentityChangeMailsAsync(
                request.UserId, targetFullName, targetSubRole, oldEmail, newEmail, accountStatus, scope,
                rawConfirmationToken, cancellationToken)
            : DepartmentPersonnelEmails.StatusNotRequired;

        return new UpdateDepartmentPersonnelResponse
        {
            Success = true,
            UserId = request.UserId,
            FullName = targetFullName,
            Email = newEmail,
            Phone = newPhone,
            Gender = DepartmentPersonnelGenders.ToWire(newGender),
            Status = accountStatus,
            Changed = true,
            EmailChanged = emailChanged,
            ConfirmationReissued = confirmationReissued,
            AuthenticationRelinkRequired = relinkRequired,
            RevokedSessions = revokedSessions,
            EmailNotificationStatus = emailStatus,
            Message = BuildMessage(emailChanged, confirmationReissued, accountStatus, emailStatus),
        };
    }

    /// <summary>
    /// Uniqueness across BOTH identity surfaces (spec §12.5): no other <c>users</c> row may own the
    /// address, and no other account's auth-provider row may be bound to it either — an SSO identity
    /// pointing at the same address would let two accounts collide on the next login. The database
    /// unique indexes remain the final guard for a race that slips past these reads.
    /// </summary>
    private async Task EnsureEmailIsFreeAsync(string newEmail, ulong targetUserId, CancellationToken cancellationToken)
    {
        var takenByUser = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Email == newEmail && u.UserId != targetUserId, cancellationToken);
        if (takenByUser)
            throw new ConflictException(
                AccountIdentityRules.EmailAlreadyUsedMessage,
                DepartmentLeaderErrorCodes.AccountEmailAlreadyExists);

        var takenByProvider = await _db.UserAuthProviders.AsNoTracking()
            .AnyAsync(p => p.ProviderEmail == newEmail && p.UserId != targetUserId, cancellationToken);
        if (takenByProvider)
            throw new ConflictException(
                "Email này đang được liên kết với phương thức đăng nhập của một tài khoản khác.",
                DepartmentLeaderErrorCodes.AuthIdentityConflict);
    }

    /// <summary>
    /// Re-points the account's login methods at the new address. Returns true when an external
    /// (SSO/FEID) link was removed and therefore has to be re-established on the next login.
    ///
    /// SSO/FEID rows are DELETED rather than edited: <c>provider_subject</c> identifies the OLD
    /// external identity and cannot be rewritten to mean the new one, the database rejects a
    /// subject-less SSO row outright, and deleting frees the subject from its unique index so the
    /// account can re-link cleanly. Local-password rows keep their hash — changing an email is not a
    /// password reset (spec §12.10).
    /// </summary>
    private bool ResetExternalIdentity(User user, string newEmail)
    {
        var external = user.AuthProviders
            .Where(p => p.ProviderType == ProviderTypes.GoogleSso || p.ProviderType == ProviderTypes.FeId)
            .ToList();

        if (external.Count > 0) _db.UserAuthProviders.RemoveRange(external);

        foreach (var provider in user.AuthProviders)
        {
            if (provider.ProviderType == ProviderTypes.GoogleSso || provider.ProviderType == ProviderTypes.FeId)
                continue;
            provider.ProviderEmail = newEmail;
        }

        return external.Count > 0;
    }

    /// <summary>Marks any lingering PENDING confirmation as SUPERSEDED so its link stops working.</summary>
    private async Task SupersedeStrayPendingConfirmationsAsync(
        ulong userId, DateTime now, CancellationToken cancellationToken)
    {
        var stray = await _db.AccountEmailConfirmations
            .Where(c => c.UserId == userId && c.Status == AccountEmailConfirmationStatuses.Pending)
            .ToListAsync(cancellationToken);

        foreach (var row in stray)
        {
            row.Status = AccountEmailConfirmationStatuses.Superseded;
            row.UpdatedAt = now;
        }
    }

    private static string ResolveAuditAction(bool emailChanged, string accountStatus)
    {
        if (!emailChanged) return DepartmentPersonnelAuditActions.UpdatePersonnel;

        return accountStatus == UserStatuses.PendingEmailConfirmation
            ? DepartmentPersonnelAuditActions.CorrectPendingPersonnelEmail
            : DepartmentPersonnelAuditActions.UpdatePersonnelIdentity;
    }

    /// <summary>
    /// Sends the neutral notice to the OLD address and the appropriate message to the NEW one, then
    /// reports the combined outcome truthfully. Each send is independently guarded so a failure on one
    /// still attempts the other; neither can throw, because the identity change is already committed.
    /// </summary>
    private async Task<string> SendIdentityChangeMailsAsync(
        ulong targetUserId,
        string fullName,
        string? subRole,
        string oldEmail,
        string newEmail,
        string accountStatus,
        DepartmentLeaderScope scope,
        string? rawConfirmationToken,
        CancellationToken cancellationToken)
    {
        // The unlinked address gets the SAME anonymous notice the account-management edit path sends:
        // it may belong to somebody reached by a typo, so it carries no name, no new address and no
        // role — which is exactly why these two templates declare no variables at all.
        var pending = accountStatus == UserStatuses.PendingEmailConfirmation && rawConfirmationToken is not null;

        var oldOk = await TrySendAsync(new SystemEmailRequest(
            pending
                ? SystemEmailTemplates.AccountPendingEmailChangedOldNotice
                : SystemEmailTemplates.AccountEmailChangedOldNotice,
            new EmailRecipient(oldEmail),
            new Dictionary<string, string>(),
            RelatedType: "User",
            RelatedId: targetUserId,
            SentBy: scope.ActorUserId), cancellationToken);

        bool newOk;
        if (pending)
        {
            // Still unconfirmed — the new address gets a live confirmation link, not a "your login
            // changed" notice.
            newOk = await TrySendAsync(new SystemEmailRequest(
                SystemEmailTemplates.AccountEmailConfirmation,
                new EmailRecipient(newEmail, fullName),
                await AccountEmailVariables.ForConfirmationAsync(
                    _db, fullName, RoleCodes.Department, subRole,
                    scope.CampusId, _confirmations.ExpiryHours, cancellationToken),
                TrustedBlocks: AccountEmailVariables.ConfirmationBlocks(
                    _confirmations.BuildConfirmUrl(rawConfirmationToken!)),
                RelatedType: "User",
                RelatedId: targetUserId,
                SentBy: scope.ActorUserId), cancellationToken);
        }
        else
        {
            newOk = await TrySendAsync(new SystemEmailRequest(
                SystemEmailTemplates.AccountEmailChangedNewNotice,
                new EmailRecipient(newEmail, fullName),
                new Dictionary<string, string>
                {
                    ["fullName"] = fullName,
                    // Masked, not in full: the holder is entitled to know WHICH address was unlinked,
                    // and seeing part of it is enough to recognise it.
                    ["oldEmailMasked"] = DepartmentPersonnelAudit.MaskEmail(oldEmail),
                },
                RelatedType: "User",
                RelatedId: targetUserId,
                SentBy: scope.ActorUserId), cancellationToken);
        }

        return (oldOk, newOk) switch
        {
            (true, true) => DepartmentPersonnelEmails.StatusSent,
            (false, false) => DepartmentPersonnelEmails.StatusFailed,
            _ => DepartmentPersonnelEmails.StatusPartial,
        };
    }

    private async Task<bool> TrySendAsync(SystemEmailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dispatcher.SendAsync(request, cancellationToken);
            return result.Delivery.Status == EmailDeliveryStatus.Sent;
        }
        catch
        {
            return false; // the account is already saved and must not be rolled back
        }
    }

    private static string BuildMessage(
        bool emailChanged, bool confirmationReissued, string accountStatus, string emailStatus)
    {
        if (!emailChanged) return "Đã cập nhật thông tin nhân sự.";

        var deliveryNote = emailStatus switch
        {
            DepartmentPersonnelEmails.StatusSent => string.Empty,
            DepartmentPersonnelEmails.StatusPartial => " Một số thông báo email chưa gửi được.",
            _ => " Chưa gửi được thông báo email.",
        };

        if (confirmationReissued)
            return "Đã cập nhật thông tin nhân sự. Liên kết xác nhận cũ đã hết hiệu lực và liên kết mới "
                   + "đã được gửi tới email mới; tài khoản vẫn ở trạng thái chờ xác nhận." + deliveryNote;

        var statusNote = accountStatus switch
        {
            UserStatuses.Inactive => " Tài khoản vẫn đang bị vô hiệu hóa.",
            UserStatuses.Locked => " Tài khoản vẫn đang bị khóa.",
            _ => string.Empty,
        };

        return "Đã cập nhật thông tin nhân sự. Email đăng nhập đã thay đổi và các phiên hiện tại đã bị thu hồi."
               + statusNote + deliveryNote;
    }
}

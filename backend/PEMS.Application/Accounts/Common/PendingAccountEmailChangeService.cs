using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// What a prepared pending-email change produced, for the caller to build its emails from. The raw
/// token is here and NOWHERE else — it is never persisted, logged or audited; only its hash reaches
/// the database.
/// </summary>
/// <param name="OldEmail">The address the account had before the change (the neutral notice goes here).</param>
/// <param name="NewEmail">The normalized new address the confirmation link is bound to.</param>
/// <param name="OldFullName">The name before the change, for the audit entry.</param>
/// <param name="NewFullName">The name after it — what the confirmation email should greet.</param>
/// <param name="RawToken">The one-time token to embed in the confirmation URL.</param>
public sealed record PreparedPendingEmailChange(
    string OldEmail,
    string NewEmail,
    string OldFullName,
    string NewFullName,
    string RawToken);

/// <summary>
/// The one place that knows how to move a PENDING_EMAIL_CONFIRMATION account onto a different address.
///
/// <para>
/// Two flows need it — HO's dedicated "sửa email" (<c>EditPendingAccountEmailCommandHandler</c>) and a
/// Staff Leader's combined role+identity edit (<c>UpdateAccountRoleCommandHandler</c>) — and they must
/// not drift apart: a pending account has never proven it owns an address, so whichever flow changes
/// that address has to re-issue the activation link, or the account is left with no way to ever log in.
/// Splitting the work across two implementations is exactly how one of them ends up forgetting a step.
/// </para>
///
/// <para>
/// The service mutates the tracked entity but deliberately does NOT save: the caller owns the
/// transaction, so the new identity, the re-pointed providers and the token that replaces the old one
/// all commit together or not at all.
/// </para>
/// </summary>
public interface IPendingAccountEmailChangeService
{
    /// <summary>
    /// Validates and applies the address change to <paramref name="user"/>: normalizes and checks the
    /// new email (and the name, when one is supplied), refuses a duplicate, re-points the local-password
    /// providers, unlinks the external ones, clears the email verification and issues a fresh
    /// confirmation token that supersedes every live one. Nothing is written to the database — the
    /// caller persists inside its own transaction.
    /// </summary>
    Task<PreparedPendingEmailChange> PrepareAsync(
        User user,
        string newEmail,
        string? newFullName,
        CancellationToken cancellationToken);
}

/// <inheritdoc cref="IPendingAccountEmailChangeService"/>
public sealed class PendingAccountEmailChangeService : IPendingAccountEmailChangeService
{
    private readonly IApplicationDbContext _db;
    private readonly IAccountEmailConfirmationService _confirmations;

    public PendingAccountEmailChangeService(
        IApplicationDbContext db, IAccountEmailConfirmationService confirmations)
    {
        _db = db;
        _confirmations = confirmations;
    }

    public async Task<PreparedPendingEmailChange> PrepareAsync(
        User user, string newEmail, string? newFullName, CancellationToken cancellationToken)
    {
        // Full name is optional; when sent it is held to the same shared rules as every other account
        // flow, so a direct API call cannot store a name the modal would have rejected.
        var oldFullName = user.FullName;
        var resolvedFullName = oldFullName;
        if (newFullName is not null)
        {
            resolvedFullName = AccountIdentityRules.NormalizeFullName(newFullName);
            if (AccountIdentityRules.ValidateFullName(resolvedFullName) is { } fullNameError)
                throw new ValidationException(fullNameError);
        }

        var normalizedEmail = AccountIdentityRules.NormalizeEmail(newEmail);
        if (AccountIdentityRules.ValidateEmail(normalizedEmail) is { } emailError)
            throw new ValidationException(emailError);

        var oldEmail = user.Email;
        if (string.Equals(normalizedEmail, oldEmail, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Email mới trùng với email hiện tại.", "EMAIL_UNCHANGED");

        if (await _db.Users.AsNoTracking()
                .AnyAsync(u => u.Email == normalizedEmail && u.UserId != user.UserId, cancellationToken))
            throw new ConflictException(
                AccountIdentityRules.EmailAlreadyUsedMessage, AccountErrorCodes.EmailAlreadyExists);

        user.FullName = resolvedFullName;
        user.Email = normalizedEmail;

        await AccountAuthProviderSync.RepointAsync(_db, user.UserId, cancellationToken);

        // Fresh token bound to the NEW email. Supersedes every live token for this user, so the link
        // already mailed to the old address stops confirming. isResend:false — a corrected address
        // starts its own series rather than continuing the failed attempts at the previous one, which
        // is also what keeps the resend cap from carrying over onto an address that never saw a mail.
        var rawToken = await _confirmations.IssuePendingAsync(
            user.UserId, normalizedEmail, isResend: false, cancellationToken);

        return new PreparedPendingEmailChange(
            oldEmail, normalizedEmail, oldFullName, resolvedFullName, rawToken);
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// The two messages a pending account's address change produces, built the same way for every flow
/// that performs one (HO's "sửa email", a Staff Leader's combined role+email edit).
///
/// <para>
/// They are built here rather than in each handler for the reason the shared
/// <see cref="IPendingAccountEmailChangeService"/> exists: the difference between these two messages is
/// the whole point of the flow, and it is invisible at the call site. The NEW address gets the same
/// <c>ACCOUNT_EMAIL_CONFIRMATION</c> the create flow sends — activation button and all — because the
/// account has still never proven it owns an address and what its holder needs is a way in, not a
/// notice that something changed. The OLD address gets a notice carrying nothing.
/// </para>
/// </summary>
public static class PendingAccountEmailChangeMails
{
    /// <summary>
    /// The activation mail for the NEW address. Every variable describes the account as it stands AFTER
    /// the change — when a role and an email move in the same request, the confirmation must state the
    /// role the holder is actually being activated into, not the one they were created with.
    /// </summary>
    public static async Task<SystemEmailRequest> BuildConfirmationAsync(
        IApplicationDbContext db,
        IAccountEmailConfirmationService confirmations,
        ulong userId,
        string finalFullName,
        string finalRoleCode,
        string? finalSubRole,
        ulong? finalCampusId,
        string newEmail,
        string rawToken,
        ulong? sentBy,
        CancellationToken cancellationToken)
        => new SystemEmailRequest(
            SystemEmailTemplates.AccountEmailConfirmation,
            new EmailRecipient(newEmail, finalFullName),
            await AccountEmailVariables.ForConfirmationAsync(
                db, finalFullName, finalRoleCode, finalSubRole, finalCampusId,
                confirmations.ExpiryHours, cancellationToken),
            TrustedBlocks: AccountEmailVariables.ConfirmationBlocks(confirmations.BuildConfirmUrl(rawToken)),
            RelatedType: "User",
            RelatedId: userId,
            SentBy: sentBy);

    /// <summary>
    /// The neutral notice for the address being left behind.
    ///
    /// <para>
    /// Deliberately anonymous: the address being unlinked may have been a typo belonging to somebody
    /// with no connection to this account. No variables — no holder name, no new address, no role,
    /// campus or department, and certainly no token or confirmation URL — and no display name on the
    /// envelope either, since a To header reading "Nguyễn Văn A &lt;stranger@…&gt;" would name the
    /// holder just as effectively as the body would.
    /// </para>
    /// </summary>
    public static SystemEmailRequest BuildOldAddressNotice(ulong userId, string oldEmail, ulong? sentBy)
        => new SystemEmailRequest(
            SystemEmailTemplates.AccountPendingEmailChangedOldNotice,
            new EmailRecipient(oldEmail),
            new Dictionary<string, string>(),
            RelatedType: "User",
            RelatedId: userId,
            SentBy: sentBy);
}

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Provisions Visitor accounts automatically when a visit-request is submitted.
/// If the email already exists the existing userId is returned without modification.
/// </summary>
public interface IUserProvisionService
{
    /// <summary>
    /// Ensures a Visitor-role account exists for <paramref name="email"/>.
    /// Creates one (Role = VISITOR, CreatedVia = VISITOR_FORM) if not found.
    /// Returns the <c>UserId</c> of the existing or newly created account.
    /// </summary>
    /// <remarks>
    /// Only ever called for the REGISTRANT: operational contacts are invited, never provisioned.
    /// If the email belongs to an existing non-VISITOR account the role is NOT changed and a
    /// <c>ConflictException</c> (REGISTRANT_EMAIL_BELONGS_TO_INTERNAL_ACCOUNT) is thrown — the
    /// contact's code used to come out of this backstop, which would have pointed a client at the
    /// wrong input. If it belongs to a VISITOR account that is not ACTIVE a
    /// <c>BusinessRuleException</c> (VISITOR_ACCOUNT_INACTIVE) is thrown.
    ///
    /// <para>
    /// When the account already exists, <paramref name="phone"/> and <paramref name="nationality"/>
    /// only ever BACKFILL a currently-empty field — a value the account already has (typed by the
    /// person themselves, e.g. via a later profile edit or an earlier submission) is never
    /// overwritten by a new form snapshot.
    /// </para>
    /// </remarks>
    Task<ulong> EnsureVisitorAccountAsync(
        string email,
        string fullName,
        string? phone,
        string? nationality,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only pre-check for the initiate step: verifies the contact email can be used to
    /// create/link a VISITOR account WITHOUT provisioning anything, so the request can be
    /// rejected before an OTP is sent. A non-existent email is allowed (it will be created later).
    /// Throws a <c>ConflictException</c> (CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT) for a
    /// non-VISITOR account and a <c>BusinessRuleException</c> (VISITOR_ACCOUNT_INACTIVE) for an
    /// inactive one. Callers turn these into per-campus field errors
    /// (<c>CampusVisits[i].OperationalContact.Email</c>) — one address may be named by several
    /// campuses, and only the caller knows which.
    /// </summary>
    Task ValidateContactEmailCanBeUsedForVisitorAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only pre-check for the PUBLIC flow registrant email: an email that belongs to an
    /// internal (non-VISITOR) account cannot go through OTP provisioning. Throws a
    /// <c>ConflictException</c> (REGISTRANT_EMAIL_BELONGS_TO_INTERNAL_ACCOUNT) in that case and a
    /// <c>BusinessRuleException</c> (VISITOR_ACCOUNT_INACTIVE) for a non-ACTIVE VISITOR account.
    /// A non-existent email is allowed (a VISITOR account is created at verify time).
    /// </summary>
    Task ValidateRegistrantEmailUsableForPublicFlowAsync(
        string email,
        CancellationToken cancellationToken = default);
}

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
    /// If the email belongs to an existing non-VISITOR account the role is NOT changed and a
    /// <c>ConflictException</c> (CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT) is thrown.
    /// If it belongs to a VISITOR account that is not ACTIVE a <c>BusinessRuleException</c>
    /// (VISITOR_ACCOUNT_INACTIVE) is thrown.
    /// </remarks>
    Task<ulong> EnsureVisitorAccountAsync(
        string email,
        string fullName,
        string? phone,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only pre-check for the initiate step: verifies the contact email can be used to
    /// create/link a VISITOR account WITHOUT provisioning anything, so the request can be
    /// rejected before an OTP is sent. A non-existent email is allowed (it will be created later).
    /// Throws the same exceptions as <see cref="EnsureVisitorAccountAsync"/> for a non-VISITOR
    /// or inactive-VISITOR account.
    /// </summary>
    Task ValidateContactEmailCanBeUsedForVisitorAsync(
        string email,
        CancellationToken cancellationToken = default);
}

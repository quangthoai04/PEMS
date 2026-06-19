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
    Task<ulong> EnsureVisitorAccountAsync(
        string email,
        string fullName,
        string? phone,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

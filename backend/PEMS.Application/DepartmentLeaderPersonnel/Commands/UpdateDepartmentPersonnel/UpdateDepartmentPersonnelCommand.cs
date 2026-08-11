using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.UpdateDepartmentPersonnel;

/// <summary>
/// Spec §12 — edits a department member's profile AND login identity in one command.
///
/// One command rather than a profile edit plus a separate "change email" call, because the two must
/// commit together: a partial apply could leave the account renamed but still signing in with the old
/// address, or the reverse. Role, sub-role, department, campus and status are absent from the payload
/// entirely — this endpoint cannot move or re-role anyone (spec §12.1).
/// </summary>
public sealed class UpdateDepartmentPersonnelCommand : IRequest<UpdateDepartmentPersonnelResponse>
{
    /// <summary>Bound from the route, never from the body.</summary>
    public ulong UserId { get; set; }

    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>MALE / FEMALE / OTHER.</summary>
    public string? Gender { get; init; }
}

/// <summary>
/// Truthful outcome of the edit. The frontend renders from these flags — never from the HTTP status
/// alone (spec §5.6): a 200 with <c>changed: false</c> means nothing was modified, and
/// <c>emailNotificationStatus</c> may report a failure even though the identity change committed.
/// </summary>
public sealed class UpdateDepartmentPersonnelResponse
{
    public bool Success { get; init; }
    public ulong UserId { get; init; }

    public required string FullName { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public string? Gender { get; init; }

    /// <summary>The account status AFTER the edit — always identical to the status before it.</summary>
    public required string Status { get; init; }

    /// <summary>False when every submitted value already matched the stored record.</summary>
    public bool Changed { get; init; }

    public bool EmailChanged { get; init; }

    /// <summary>True when a PENDING account received a fresh confirmation link bound to the new address.</summary>
    public bool ConfirmationReissued { get; init; }

    /// <summary>True when Google SSO rows were removed and must be re-linked on the next login.</summary>
    public bool AuthenticationRelinkRequired { get; init; }

    public int RevokedSessions { get; init; }

    /// <summary>SENT / PARTIAL / FAILED / SKIPPED / NOT_REQUIRED.</summary>
    public required string EmailNotificationStatus { get; init; }

    public required string Message { get; init; }
}

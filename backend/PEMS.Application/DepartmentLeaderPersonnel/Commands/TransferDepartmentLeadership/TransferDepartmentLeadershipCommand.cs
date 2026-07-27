using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.TransferDepartmentLeadership;

/// <summary>
/// Spec §16 — hands the department over to one of its own staff members.
///
/// There is no <c>departmentId</c>: the department is the caller's, resolved from the verified scope.
/// The only input is who takes over.
/// </summary>
public sealed class TransferDepartmentLeadershipCommand : IRequest<TransferDepartmentLeadershipResponse>
{
    public ulong NewLeaderUserId { get; init; }
}

public sealed class TransferDepartmentLeadershipResponse
{
    public bool Success { get; init; }
    public ulong DepartmentId { get; init; }

    public ulong PreviousLeaderUserId { get; init; }
    public required string PreviousLeaderName { get; init; }

    public ulong NewLeaderUserId { get; init; }
    public required string NewLeaderName { get; init; }

    /// <summary>Sessions revoked across BOTH accounts — each must sign in again for a new token.</summary>
    public int RevokedSessions { get; init; }

    /// <summary>
    /// True — the caller is no longer a Department Leader and their current token is void. The
    /// frontend uses this to sign the outgoing leader out instead of leaving them on a dead screen.
    /// </summary>
    public bool ActorMustSignInAgain { get; init; }

    /// <summary>SENT / PARTIAL / FAILED / SKIPPED.</summary>
    public required string EmailNotificationStatus { get; init; }

    public required string Message { get; init; }
}

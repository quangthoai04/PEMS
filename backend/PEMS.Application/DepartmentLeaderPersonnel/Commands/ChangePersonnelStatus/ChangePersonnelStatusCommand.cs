using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.ChangePersonnelStatus;

/// <summary>
/// Spec §15 — enables or disables a member of the caller's department.
///
/// Named for what it does. The old <c>RemovePersonnel</c> naming described a deletion that never
/// happened: nobody is removed from the department and no row is deleted — only
/// <c>users.status</c> moves between ACTIVE and INACTIVE (spec §6/§15).
/// </summary>
public sealed class ChangePersonnelStatusCommand : IRequest<ChangePersonnelStatusResponse>
{
    /// <summary>Bound from the route, never from the body.</summary>
    public ulong UserId { get; set; }

    /// <summary>ACTIVE or INACTIVE. Nothing else is reachable through this endpoint.</summary>
    public string? TargetStatus { get; init; }

    /// <summary>Operator-supplied justification, recorded in the audit trail.</summary>
    public string? Reason { get; init; }
}

public sealed class ChangePersonnelStatusResponse
{
    public bool Success { get; init; }
    public ulong UserId { get; init; }
    public required string PreviousStatus { get; init; }
    public required string Status { get; init; }

    /// <summary>Sessions terminated by a disable. Always 0 for an enable — access is not restored.</summary>
    public int RevokedSessions { get; init; }

    /// <summary>SENT / SKIPPED / FAILED / NOT_REQUIRED.</summary>
    public required string EmailNotificationStatus { get; init; }

    public required string Message { get; init; }
}

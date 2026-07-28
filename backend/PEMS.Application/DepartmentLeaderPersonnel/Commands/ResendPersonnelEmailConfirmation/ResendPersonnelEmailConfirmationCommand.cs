using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.ResendPersonnelEmailConfirmation;

/// <summary>
/// Spec §13 — re-issues the confirmation link for a pending member of the caller's department.
///
/// The command carries the target id and nothing else: the address the link is sent to is read from
/// the database, never accepted from the client. Letting a caller pass an address here would turn a
/// "resend" into an unaudited email change.
/// </summary>
public sealed record ResendPersonnelEmailConfirmationCommand(ulong UserId)
    : IRequest<ResendPersonnelEmailConfirmationResponse>;

public sealed class ResendPersonnelEmailConfirmationResponse
{
    public bool Success { get; init; }
    public ulong UserId { get; init; }

    /// <summary>The address the link was sent to — read from the database.</summary>
    public required string Email { get; init; }

    /// <summary>SENT / SKIPPED / FAILED.</summary>
    public required string EmailNotificationStatus { get; init; }

    public int ResendCount { get; init; }
    public required string Message { get; init; }
}

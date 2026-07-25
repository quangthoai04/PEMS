using MediatR;

namespace PEMS.Application.Accounts.Commands.EditPendingAccountEmail;

/// <summary>
/// Corrects the email of a still-pending account (e.g. a typo). Only allowed while the account is pending;
/// it revokes the old token, updates the address, and sends a fresh confirmation to the new email. Admin-only.
/// </summary>
public sealed class EditPendingAccountEmailCommand : IRequest<EditPendingAccountEmailResponse>
{
    public ulong UserId { get; set; }
    public string NewEmail { get; set; } = default!;
}

public sealed class EditPendingAccountEmailResponse
{
    public bool Success { get; init; }
    public string Email { get; init; } = default!;
    public string EmailNotificationStatus { get; init; } = default!;   // SENT | SKIPPED | FAILED
    public string Message { get; init; } = default!;
}

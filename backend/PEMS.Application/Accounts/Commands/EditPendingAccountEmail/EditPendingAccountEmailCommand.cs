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

    /// <summary>
    /// New full name, when the caller's form edits it alongside the address. Optional — omit/null to
    /// keep the current name.
    ///
    /// <para>
    /// It rides on THIS command rather than a separate basic-info call because the edit modal offers
    /// both fields at once: two requests can half-succeed, leaving the name saved against an address
    /// that was never changed (or the reverse). Here both land in one transaction, and the
    /// confirmation email that goes out afterwards carries the new name.
    /// </para>
    /// </summary>
    public string? FullName { get; set; }
}

public sealed class EditPendingAccountEmailResponse
{
    public bool Success { get; init; }
    public string Email { get; init; } = default!;
    public string EmailNotificationStatus { get; init; } = default!;   // SENT | SKIPPED | FAILED
    public string Message { get; init; } = default!;
}

using MediatR;

namespace PEMS.Application.Accounts.Commands.ResendAccountEmailConfirmation;

/// <summary>Re-issues the email-confirmation link for a still-pending account (rate-limited). Admin-only.</summary>
public sealed class ResendAccountEmailConfirmationCommand : IRequest<ResendAccountEmailConfirmationResponse>
{
    public ulong UserId { get; set; }
}

public sealed class ResendAccountEmailConfirmationResponse
{
    public bool Success { get; init; }
    public string EmailNotificationStatus { get; init; } = default!;   // SENT | SKIPPED | FAILED
    public int ResendCount { get; init; }
    public string Message { get; init; } = default!;
}

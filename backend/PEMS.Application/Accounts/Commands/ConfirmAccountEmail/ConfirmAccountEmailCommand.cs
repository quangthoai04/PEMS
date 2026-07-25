using MediatR;

namespace PEMS.Application.Accounts.Commands.ConfirmAccountEmail;

/// <summary>
/// Public (no-login) confirmation of a pending account's email ownership (P0 #1). Side-effecting, so the
/// endpoint is POST only. The raw token is validated by hash; a valid, unexpired, PENDING token activates
/// the account exactly once.
/// </summary>
public sealed class ConfirmAccountEmailCommand : IRequest<ConfirmAccountEmailResponse>
{
    public string Token { get; set; } = default!;
}

public sealed class ConfirmAccountEmailResponse
{
    public bool Success { get; init; }

    /// <summary>Machine status the frontend renders from — CONFIRMED / ALREADY_CONFIRMED / INVALID / EXPIRED.</summary>
    public string Status { get; init; } = default!;

    public string Message { get; init; } = default!;
}

/// <summary>Stable confirmation outcomes. Deliberately identity-free (no email/user) to avoid enumeration.</summary>
public static class ConfirmAccountEmailStatuses
{
    public const string Confirmed = "CONFIRMED";
    public const string AlreadyConfirmed = "ALREADY_CONFIRMED";
    public const string Invalid = "INVALID";
    public const string Expired = "EXPIRED";
}

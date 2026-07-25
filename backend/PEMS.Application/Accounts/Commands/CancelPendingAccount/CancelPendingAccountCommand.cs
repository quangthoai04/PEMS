using MediatR;

namespace PEMS.Application.Accounts.Commands.CancelPendingAccount;

/// <summary>
/// Cancels a still-pending account before it is ever confirmed: releases any Head slot it was reserving,
/// cancels its confirmation token(s), and deactivates the account. Admin-only.
/// </summary>
public sealed class CancelPendingAccountCommand : IRequest<CancelPendingAccountResponse>
{
    public ulong UserId { get; set; }
}

public sealed class CancelPendingAccountResponse
{
    public bool Success { get; init; }
    public bool ReleasedHeadReservation { get; init; }
    public string Message { get; init; } = default!;
}

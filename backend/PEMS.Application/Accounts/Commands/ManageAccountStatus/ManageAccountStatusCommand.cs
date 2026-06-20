using MediatR;

namespace PEMS.Application.Accounts.Commands.ManageAccountStatus;

/// <summary>
/// UC-97 Manage Account Status. An ADMIN/HO (system-wide) or a Staff Leader (own campus
/// only) activates, deactivates (INACTIVE) or locks (LOCKED) a user account. When the
/// account moves from ACTIVE to a non-ACTIVE status, every active session of that user is
/// revoked so old access/refresh tokens stop working immediately.
/// </summary>
public sealed class ManageAccountStatusCommand : IRequest<ManageAccountStatusResponse>
{
    /// <summary>Target user whose status is being changed.</summary>
    public ulong UserId { get; set; }

    /// <summary>New status: ACTIVE | INACTIVE | LOCKED (mirrors users.status ENUM).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Optional human-readable reason recorded in the audit log.</summary>
    public string? Reason { get; set; }
}

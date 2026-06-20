namespace PEMS.Application.Accounts.Commands.ManageAccountStatus;

public sealed class ManageAccountStatusResponse
{
    public ulong UserId { get; init; }

    /// <summary>The status now persisted on the account (ACTIVE | INACTIVE | LOCKED).</summary>
    public string Status { get; init; } = default!;

    /// <summary>How many active sessions were revoked as a result of this change.</summary>
    public int RevokedSessions { get; init; }

    public string Message { get; init; } = "Account status updated successfully.";
}

namespace PEMS.Application.Accounts.Commands.UpdateAccountRole;

public sealed class UpdateAccountRoleResponse
{
    public ulong UserId { get; init; }
    public string RoleCode { get; init; } = default!;
    public ulong? PrimaryCampusId { get; init; }
    public int RevokedSessions { get; init; }
    public string Message { get; init; } =
        "Role updated successfully. The user must sign in again via the internal portal and select the correct campus.";
}

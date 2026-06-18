namespace PEMS.Application.Accounts.Commands.UpdateAccountRole;

public sealed class UpdateAccountRoleResponse
{
    public string UserId { get; init; } = default!;
    public string RoleCode { get; init; } = default!;
    public string? PrimaryCampusId { get; init; }
    public int RevokedSessions { get; init; }
    public string Message { get; init; } =
        "Role updated successfully. The user must sign in again via the internal portal and select the correct campus.";
}

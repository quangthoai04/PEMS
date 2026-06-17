namespace PEMS.Application.Authentication.Models;

/// <summary>
/// A single permission entry for the authenticated user's role, used by the
/// frontend to drive menu / action visibility.
/// </summary>
public sealed class UserPermissionDto
{
    public string PermissionCode { get; init; } = null!;
    public string PermissionLevel { get; init; } = null!;
    public string PermissionGroup { get; init; } = null!;
}

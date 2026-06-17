namespace PEMS.Application.Authentication.Models;

/// <summary>Response for GET /api/auth/permissions.</summary>
public sealed class PermissionsResponse
{
    public string RoleCode { get; init; } = null!;
    public IReadOnlyList<UserPermissionDto> Permissions { get; init; } = new List<UserPermissionDto>();
}

namespace PEMS.Application.Authentication.Models;

/// <summary>Response for GET /api/auth/me — profile + permissions.</summary>
public sealed class UserProfileResponse
{
    public AuthUserDto User { get; init; } = null!;
    public IReadOnlyList<UserPermissionDto> Permissions { get; init; } = new List<UserPermissionDto>();
}

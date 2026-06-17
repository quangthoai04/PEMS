namespace PEMS.Application.Authentication.Models;

/// <summary>
/// Standard response for successful credential / SSO login and token refresh.
/// </summary>
public sealed class AuthResponse
{
    public string AccessToken { get; init; } = null!;

    /// <summary>Raw refresh token. Only stored hashed server-side; shown once to the client.</summary>
    public string RefreshToken { get; init; } = null!;

    public DateTime ExpiresAt { get; init; }

    public AuthUserDto User { get; init; } = null!;

    public IReadOnlyList<UserPermissionDto> Permissions { get; init; } = new List<UserPermissionDto>();
}

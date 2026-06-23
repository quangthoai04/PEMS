namespace PEMS.Application.Authentication.Models;

/// <summary>Response for GET /api/auth/me — profile only.</summary>
public sealed class UserProfileResponse
{
    public AuthUserDto User { get; init; } = null!;
}

namespace PEMS.Application.Authentication.Models;

/// <summary>
/// Safe user projection returned to the client after authentication.
/// Never contains password hashes, token hashes or provider subjects.
/// </summary>
public sealed class AuthUserDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string? Phone { get; init; }
    public string? AvatarUrl { get; init; }

    public string RoleCode { get; init; } = null!;
    public string? RoleName { get; init; }
    public string? SubRole { get; init; }

    public ulong? PrimaryCampusId { get; init; }
    public string? CampusCode { get; init; }
    public string? CampusName { get; init; }

    public ulong? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
}

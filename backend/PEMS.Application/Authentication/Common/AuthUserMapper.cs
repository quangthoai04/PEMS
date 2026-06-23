using PEMS.Application.Authentication.Models;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Authentication.Common;

/// <summary>Maps a <see cref="User"/> to the safe <see cref="AuthUserDto"/> projection.</summary>
public static class AuthUserMapper
{
    /// <summary>
    /// Builds the DTO. For full campus/department names, the caller should have
    /// loaded <c>Role</c>, <c>PrimaryCampus</c> and <c>Department</c> navigations.
    /// </summary>
    public static AuthUserDto ToDto(User user) => new()
    {
        UserId = user.UserId,
        FullName = user.FullName,
        Email = user.Email,
        Phone = user.Phone,
        AvatarUrl = user.AvatarUrl,
        RoleCode = user.Role?.RoleCode ?? string.Empty,
        RoleName = user.Role?.Name,
        SubRole = user.SubRole,
        PrimaryCampusId = user.PrimaryCampusId,
        CampusCode = user.PrimaryCampus?.CampusCode,
        CampusName = user.PrimaryCampus?.Name,
        DepartmentId = user.DepartmentId,
        DepartmentName = user.Department?.Name,
        EffectiveRole = PEMS.Application.Common.Security.EffectiveRole.Resolve(user.Role?.RoleCode ?? string.Empty, user.SubRole),
        Status = user.Status
    };
}

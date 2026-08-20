using System;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Authentication.Common;

/// <summary>Maps a <see cref="User"/> to the safe <see cref="AuthUserDto"/> projection.</summary>
public static class AuthUserMapper
{
    /// <summary>
    /// Builds the DTO. For full campus/department names, the caller should have
    /// loaded <c>Role</c>, <c>PrimaryCampus</c> and <c>Department</c> navigations.
    /// </summary>
    public static AuthUserDto ToDto(User user)
    {
        // Patch 7 (P7.1): fail closed the same way every other caller of EffectiveRole.Resolve
        // already does (RoleAuthorizeAttribute, RoleAccessPolicy, DepartmentPersonnelManagementScope)
        // — an account whose (role_code, sub_role) pair is not a valid combination is a data defect,
        // not a server fault, and must never surface as a raw 500 out of login/GetCurrentUser/refresh.
        string effectiveRole;
        try
        {
            effectiveRole = EffectiveRole.Resolve(user.Role?.RoleCode ?? string.Empty, user.SubRole);
        }
        catch (InvalidOperationException)
        {
            throw new ForbiddenException(
                "Tài khoản đang gặp lỗi cấu hình vai trò. Vui lòng liên hệ quản trị viên.",
                AuthErrorCodes.InvalidRoleCombination);
        }

        return new AuthUserDto
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
            EffectiveRole = effectiveRole,
            Status = user.Status,
        };
    }
}

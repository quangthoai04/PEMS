using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;

namespace PEMS.Application.ApiIntegrations.Common;

/// <summary>
/// Fixed policy of the API Configuration module: ADMIN manages everything;
/// HO gets read-only monitoring (status/logs/quota, never credentials).
/// </summary>
public static class ApiIntegrationAccess
{
    public static bool CanManage(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.RoleCode)) return false;
        try { return EffectiveRole.Resolve(user.RoleCode, user.SubRole) == EffectiveRole.Admin; }
        catch { return false; }
    }

    public static bool CanRead(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.RoleCode)) return false;
        try
        {
            var role = EffectiveRole.Resolve(user.RoleCode, user.SubRole);
            return role is EffectiveRole.Admin or EffectiveRole.Ho;
        }
        catch { return false; }
    }

    public static void EnsureManage(ICurrentUserService user)
    {
        if (!CanManage(user))
            throw new AuthBusinessException(ApiIntegrationErrorCodes.Forbidden,
                "Chỉ ADMIN mới được quản lý cấu hình API.", 403);
    }

    public static void EnsureRead(ICurrentUserService user)
    {
        if (!CanRead(user))
            throw new AuthBusinessException(ApiIntegrationErrorCodes.Forbidden,
                "Bạn không có quyền xem cấu hình API.", 403);
    }
}

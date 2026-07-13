using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;

namespace PEMS.Application.Admin.Common;

/// <summary>
/// Fixed policy of the System Administration Console: every /api/admin/* handler
/// re-checks that the caller is ADMIN (defense in depth — the controller is also
/// gated by [RoleAuthorize(EffectiveRole.Admin)]).
/// </summary>
public static class AdminAccess
{
    public static bool IsAdmin(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.RoleCode)) return false;
        try { return EffectiveRole.Resolve(user.RoleCode, user.SubRole) == EffectiveRole.Admin; }
        catch { return false; }
    }

    public static void EnsureAdmin(ICurrentUserService user)
    {
        if (!IsAdmin(user))
            throw new ForbiddenException("Chỉ ADMIN mới được truy cập khu vực quản trị hệ thống.");
    }
}

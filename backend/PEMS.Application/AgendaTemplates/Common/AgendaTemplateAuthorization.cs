using System;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.AgendaTemplates.Common;

/// <summary>
/// Scope-based authorization for the agenda template module (no dynamic permissions in v10):
/// GLOBAL scope is managed by HO; a campus scope is managed by the Staff Leader of that campus
/// (HO is allowed on any scope). Backend always derives/validates scope from the authenticated
/// user — the client-supplied campusId is never trusted as authorization.
/// </summary>
public static class AgendaTemplateAuthorization
{
    public static bool IsHo(ICurrentUserService u) => u.RoleCode == RoleCodes.Ho;

    public static bool IsStaffLeader(ICurrentUserService u) =>
        u.RoleCode == RoleCodes.Staff
        && string.Equals(u.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase);

    /// <summary>Throws unless the caller may create/update/delete a template or default in the
    /// given scope (null campusId = GLOBAL).</summary>
    public static void EnsureCanManageScope(ICurrentUserService u, ulong? campusId)
    {
        if (!u.IsAuthenticated || u.UserId is null)
            throw new ForbiddenException();

        if (campusId is null)
        {
            if (!IsHo(u))
                throw new ForbiddenException("Chỉ HO mới được quản lý mẫu agenda phạm vi toàn hệ thống (GLOBAL).");
            return;
        }

        bool ok = IsHo(u) || (IsStaffLeader(u) && u.PrimaryCampusId == campusId);
        if (!ok)
            throw new ForbiddenException("Bạn chỉ được quản lý mẫu agenda của cơ sở mình phụ trách.");
    }

    /// <summary>Throws unless the caller may use the agenda-template management screens at all.</summary>
    public static void EnsureCanViewManagement(ICurrentUserService u)
    {
        if (!u.IsAuthenticated || u.UserId is null)
            throw new ForbiddenException();
        if (!IsHo(u) && !IsStaffLeader(u))
            throw new ForbiddenException("Bạn không có quyền xem mẫu agenda.");
    }

    /// <summary>Throws unless the caller may read a specific template's scope. GLOBAL templates are
    /// readable by any HO / Staff Leader; a campus template only by HO or that campus's Staff Leader.</summary>
    public static void EnsureCanViewScope(ICurrentUserService u, ulong? campusId)
    {
        EnsureCanViewManagement(u);
        if (campusId is not null && !IsHo(u) && u.PrimaryCampusId != campusId)
            throw new ForbiddenException("Bạn chỉ được xem mẫu agenda của cơ sở mình.");
    }
}

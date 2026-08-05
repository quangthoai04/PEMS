using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using System;

namespace PEMS.Application.Common.Security;

/// <summary>
/// Coarse, cross-module role gates (authorization layer 2, mirrored in handlers).
///
/// Two rules hold everywhere in this class:
/// 1. An unresolvable (role_code, sub_role) pair denies — never throws, never grants.
/// 2. ADMIN is a technical administrator. It is granted only where a business rule says
///    so explicitly, and is never a blanket "return true" for business modules.
/// </summary>
public class RoleAccessPolicy : IRoleAccessPolicy
{
    /// <summary>
    /// Resolves the effective role without throwing. An invalid combination (e.g. STAFF with
    /// no sub_role) is a data defect: it denies access rather than bubbling an exception up
    /// as a 500, and rather than falling through to a permissive default.
    /// </summary>
    private static bool TryResolve(ICurrentUserService user, out string effectiveRole)
    {
        effectiveRole = string.Empty;
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.RoleCode)) return false;

        try
        {
            effectiveRole = EffectiveRole.Resolve(user.RoleCode, user.SubRole);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Account management surface (UC-95..UC-100). ADMIN is included deliberately: it owns
    /// account/role administration, which is a system-administration duty rather than a
    /// business one. Scope per target account is enforced by <see cref="CanManageAccount"/>.
    /// </summary>
    public bool CanAccessAccountManagement(ICurrentUserService user)
    {
        if (!TryResolve(user, out var effectiveRole)) return false;
        return effectiveRole switch
        {
            EffectiveRole.Admin => true,
            EffectiveRole.Ho => true,
            EffectiveRole.StaffLeader => true,
            _ => false
        };
    }

    public bool CanManageAccount(ICurrentUserService user, User targetAccount)
    {
        if (!CanAccessAccountManagement(user)) return false;
        if (!TryResolve(user, out var effectiveRole)) return false;

        if (effectiveRole == EffectiveRole.Admin) return true;

        if (effectiveRole == EffectiveRole.Ho)
        {
            // HO can manage anyone except ADMIN and cannot disable another HO.
            return targetAccount.Role?.RoleCode != RoleCode.Admin;
        }

        if (effectiveRole == EffectiveRole.StaffLeader)
        {
            // Staff Leader can only manage within their campus and cannot manage HO/Admin.
            if (targetAccount.Role?.RoleCode == RoleCode.Admin || targetAccount.Role?.RoleCode == RoleCode.Ho)
                return false;

            return user.PrimaryCampusId != null && user.PrimaryCampusId == targetAccount.PrimaryCampusId;
        }

        return false;
    }

    /// <summary>
    /// Campus master data is HO-only (PERMISSION_MATRIX §5.14, UC-81..UC-87, and
    /// docs/CampusManagement/00_CAMPUS_MANAGEMENT_COMMON_RULES_HO.md).
    /// ADMIN was removed: it is a technical administrator with no business access here.
    /// </summary>
    public bool CanAccessCampusManagement(ICurrentUserService user)
    {
        if (!TryResolve(user, out var effectiveRole)) return false;
        return effectiveRole == EffectiveRole.Ho;
    }

    /// <summary>
    /// Campus is master data with no per-record owner, so managing any campus is the same
    /// decision as reaching the module at all. The <paramref name="campus"/> argument is kept
    /// for callers that already pass the loaded record and for future per-campus rules.
    /// </summary>
    public bool CanManageCampus(ICurrentUserService user, Campus? campus)
    {
        return CanAccessCampusManagement(user);
    }

    public bool CanAccessDepartmentManagement(ICurrentUserService user)
    {
        if (!TryResolve(user, out var effectiveRole)) return false;
        return effectiveRole is EffectiveRole.StaffLeader or EffectiveRole.DepartmentLead or EffectiveRole.Ho;
    }

    /// <summary>
    /// ADMIN does not access visit/delegation business records at all
    /// (PERMISSION_MATRIX §5.4: "ADMIN must stay — for UC-17 to UC-41 and UC-136").
    /// </summary>
    public bool CanAccessVisitManagement(ICurrentUserService user)
    {
        if (!TryResolve(user, out var effectiveRole)) return false;
        return effectiveRole != EffectiveRole.Admin;
    }

    /// <summary>
    /// Coarse visibility for a single visit request.
    ///
    /// This used to end in a bare <c>return true</c> for Staff, Department Lead, Department and
    /// Student — "usually they can see it" — which grants every request in the system to four
    /// roles the moment any caller trusts this method. It now denies by default: a role that is
    /// not covered by an explicit scope rule below has to be granted by the module that actually
    /// knows the assignment/participation graph.
    ///
    /// Per-assignment visibility for those roles lives in the delegation read model
    /// (PEMS.Application.Delegations.Queries.ViewGuestDelegationList and the per-instance
    /// participant queries), which resolves host, participant, support-department and assigned
    /// student links. Do not re-implement that here — this stays a coarse gate.
    /// </summary>
    public bool CanViewVisitRequest(ICurrentUserService user, VisitRequest request)
    {
        if (!CanAccessVisitManagement(user)) return false;
        if (!TryResolve(user, out var effectiveRole)) return false;

        if (effectiveRole == EffectiveRole.Visitor)
        {
            return request.VisitorUserId == user.UserId;
        }

        if (effectiveRole == EffectiveRole.Ho)
        {
            // HO decides MULTI_CAMPUS and monitors SINGLE_CAMPUS read-only (business rule 2026-06).
            return true;
        }

        if (effectiveRole == EffectiveRole.StaffLeader)
        {
            // Only requests that touch the leader's own campus.
            return request.CampusInstances.Any(ci => ci.CampusId == user.PrimaryCampusId);
        }

        // Staff / Department Lead / Department / Student: no blanket read. The caller must use
        // the delegation read model, which filters by host / participant / assignment.
        return false;
    }

    public bool CanProcessVisitRequest(ICurrentUserService user, VisitRequest request)
    {
        if (!CanAccessVisitManagement(user)) return false;
        if (!TryResolve(user, out var effectiveRole)) return false;

        if (effectiveRole == EffectiveRole.Ho)
        {
            return request.VisitScope == "MULTI_CAMPUS";
        }

        if (effectiveRole == EffectiveRole.StaffLeader)
        {
            // Staff Leader processes SINGLE_CAMPUS, and only within their own campus.
            return request.VisitScope == "SINGLE_CAMPUS"
                && request.CampusInstances.Any(ci => ci.CampusId == user.PrimaryCampusId);
        }

        return false;
    }
}

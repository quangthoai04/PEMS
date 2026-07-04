using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Application.Partners.Common;

/// <summary>
/// Fixed role/scope policy of the Partner module (no dynamic permissions):
///  - ADMIN: technical admin, full read; not the business approver.
///  - HO: read-only monitor of all partners.
///  - STAFF+LEADER: sees + approves/rejects partners of own campus (owner_campus_id).
///  - STAFF+STAFF: creates partners (PENDING_APPROVAL, owner_campus_id = own campus),
///    manages contacts within campus scope.
///  - DEPARTMENT/STUDENT: read APPROVED internal partners only.
///  - VISITOR/PUBLIC: public endpoint only (APPROVED + PUBLIC).
/// </summary>
public static class PartnerAccess
{
    public static string? Effective(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || string.IsNullOrEmpty(user.RoleCode)) return null;
        try { return EffectiveRole.Resolve(user.RoleCode, user.SubRole); }
        catch { return null; }
    }

    public static bool IsAdmin(ICurrentUserService user) => Effective(user) == EffectiveRole.Admin;
    public static bool IsHo(ICurrentUserService user) => Effective(user) == EffectiveRole.Ho;
    public static bool IsStaffLeader(ICurrentUserService user) => Effective(user) == EffectiveRole.StaffLeader;
    public static bool IsStaff(ICurrentUserService user) => Effective(user) == EffectiveRole.Staff;

    /// <summary>Any internal role that may open the partner management screens.</summary>
    public static bool CanViewPartnerModule(ICurrentUserService user) =>
        Effective(user) is EffectiveRole.Admin or EffectiveRole.Ho
            or EffectiveRole.StaffLeader or EffectiveRole.Staff
            or EffectiveRole.DepartmentLead or EffectiveRole.Department or EffectiveRole.Student;

    /// <summary>Roles that see every campus (list is not campus-scoped).</summary>
    public static bool SeesAllCampuses(ICurrentUserService user) =>
        Effective(user) is EffectiveRole.Admin or EffectiveRole.Ho;

    /// <summary>Roles restricted to APPROVED + INTERNAL/PUBLIC partners only.</summary>
    public static bool ApprovedOnly(ICurrentUserService user) =>
        Effective(user) is EffectiveRole.DepartmentLead or EffectiveRole.Department or EffectiveRole.Student;

    /// <summary>IC Staff / Staff Leader may create partners.</summary>
    public static bool CanCreatePartner(ICurrentUserService user) =>
        Effective(user) is EffectiveRole.Staff or EffectiveRole.StaffLeader;

    /// <summary>Creator (own campus staff) or campus Staff Leader may edit; Admin as technical fallback.</summary>
    public static bool CanEditPartner(ICurrentUserService user, Partner partner)
    {
        var role = Effective(user);
        if (role == EffectiveRole.Admin) return true;
        if (role is EffectiveRole.Staff or EffectiveRole.StaffLeader)
            return user.PrimaryCampusId == partner.OwnerCampusId;
        return false;
    }

    /// <summary>Only the Staff Leader of the owner campus decides PENDING_APPROVAL partners.</summary>
    public static bool CanDecidePartner(ICurrentUserService user, Partner partner) =>
        IsStaffLeader(user) && user.PrimaryCampusId == partner.OwnerCampusId;

    /// <summary>Read access to one partner row for internal screens.</summary>
    public static bool CanViewPartner(ICurrentUserService user, Partner partner)
    {
        var role = Effective(user);
        if (role is EffectiveRole.Admin or EffectiveRole.Ho) return true;
        if (role is EffectiveRole.Staff or EffectiveRole.StaffLeader)
        {
            // Own-campus rows always; other campuses only once APPROVED and not PRIVATE.
            if (user.PrimaryCampusId == partner.OwnerCampusId) return true;
            return partner.ProfileStatus == PartnerProfileStatuses.Approved
                   && partner.Visibility != PartnerVisibilities.Private;
        }
        if (role is EffectiveRole.DepartmentLead or EffectiveRole.Department or EffectiveRole.Student)
            return partner.ProfileStatus == PartnerProfileStatuses.Approved
                   && partner.Visibility != PartnerVisibilities.Private;
        return false;
    }

    /// <summary>Contacts/aliases/documents mutations: campus scope (Staff/Staff Leader) or Admin.</summary>
    public static bool CanManagePartnerChildren(ICurrentUserService user, Partner partner) =>
        CanEditPartner(user, partner);
}

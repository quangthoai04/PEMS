using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Application.Partners.Common;

/// <summary>
/// Fixed role/scope policy of the Partner module (no dynamic permissions):
///  - ADMIN: technical admin. NO partner access at all — see note below.
///  - HO: read-only monitor of all partners.
///  - STAFF+LEADER: sees + approves/rejects partners of own campus (owner_campus_id).
///  - STAFF+STAFF: creates partners (PENDING_APPROVAL, owner_campus_id = own campus),
///    manages contacts within campus scope.
///  - DEPARTMENT/STUDENT: read APPROVED internal partners only.
///  - VISITOR/PUBLIC: public endpoint only (APPROVED + PUBLIC).
///
/// ADMIN was removed from every rule here. It previously had full cross-campus read plus a
/// "technical fallback" that let it edit any partner and manage that partner's contacts,
/// aliases and documents. PERMISSION_MATRIX §5.6 marks Admin "—" for UC-50..UC-54, and §4.2
/// states ADMIN is not a business superuser; a technical fallback that edits business records
/// is exactly the implicit grant that rule forbids. IsAdmin() is kept because callers may still
/// want to branch on it for non-authorization reasons, but no access decision uses it.
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
        Effective(user) is EffectiveRole.Ho
            or EffectiveRole.StaffLeader or EffectiveRole.Staff
            or EffectiveRole.DepartmentLead or EffectiveRole.Department or EffectiveRole.Student;

    /// <summary>Roles that see every campus (list is not campus-scoped).</summary>
    public static bool SeesAllCampuses(ICurrentUserService user) =>
        Effective(user) is EffectiveRole.Ho;

    /// <summary>Roles restricted to APPROVED + INTERNAL/PUBLIC partners only.</summary>
    public static bool ApprovedOnly(ICurrentUserService user) =>
        Effective(user) is EffectiveRole.DepartmentLead or EffectiveRole.Department or EffectiveRole.Student;

    /// <summary>IC Staff / Staff Leader may create partners.</summary>
    public static bool CanCreatePartner(ICurrentUserService user) =>
        Effective(user) is EffectiveRole.Staff or EffectiveRole.StaffLeader;

    /// <summary>Creator (own campus staff) or campus Staff Leader may edit. No admin fallback.</summary>
    public static bool CanEditPartner(ICurrentUserService user, Partner partner)
    {
        var role = Effective(user);
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
        if (role == EffectiveRole.Ho) return true;
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

    /// <summary>Contacts/aliases/documents mutations: campus scope (Staff/Staff Leader) only.</summary>
    public static bool CanManagePartnerChildren(ICurrentUserService user, Partner partner) =>
        CanEditPartner(user, partner);

    /// <summary>
    /// Whether a guest/minute participant may be CONFIRMED as belonging to this partner.
    ///
    /// <para>Deliberately NOT the same question as <see cref="CanViewPartner"/>. Reading a profile and
    /// asserting a business relationship against it are different acts: campus staff can see every
    /// profile of their own campus — including the ones their Staff Leader has just rejected — and
    /// reusing the read rule as the write rule is what let a REJECTED organization be linked as if it
    /// were a real partner (PART-04).</para>
    ///
    /// <para>Profile-status policy: APPROVED links freely within scope; PENDING_APPROVAL links only for
    /// the OWNER campus's own staff, so work can continue while the Staff Leader decides; DRAFT and
    /// REJECTED never link. A rejected organization is fixed by edit + resubmit, never by linking to
    /// the dead profile and never by creating a second profile under the same name.</para>
    /// </summary>
    public static bool CanLinkPartner(ICurrentUserService user, Partner partner) =>
        LinkBlockReason(user, partner) is null;

    /// <summary>
    /// The reason <paramref name="partner"/> may NOT be linked, or <c>null</c> when it may.
    /// Values come from <see cref="PartnerLinkBlockReasons"/>; the matcher hands them to the UI so a
    /// blocked candidate can explain itself instead of failing on click.
    /// </summary>
    public static string? LinkBlockReason(ICurrentUserService user, Partner partner)
    {
        // Scope first: never leak "this profile is rejected" about a partner the caller cannot see.
        if (!CanViewPartner(user, partner)) return PartnerLinkBlockReasons.OutOfScope;

        return partner.ProfileStatus switch
        {
            PartnerProfileStatuses.Approved => null,
            PartnerProfileStatuses.PendingApproval => IsOwnerCampusStaff(user, partner)
                ? null
                : PartnerLinkBlockReasons.PendingOtherCampus,
            PartnerProfileStatuses.Rejected => PartnerLinkBlockReasons.Rejected,
            PartnerProfileStatuses.Draft => PartnerLinkBlockReasons.Draft,
            _ => PartnerLinkBlockReasons.OutOfScope,
        };
    }

    /// <summary>What the UI should offer for a candidate the caller cannot link.</summary>
    public static string RecommendedActionFor(string? blockReason) => blockReason switch
    {
        null => PartnerLinkRecommendedActions.Link,
        PartnerLinkBlockReasons.Rejected => PartnerLinkRecommendedActions.Resubmit,
        _ => PartnerLinkRecommendedActions.None,
    };

    private static bool IsOwnerCampusStaff(ICurrentUserService user, Partner partner) =>
        Effective(user) is EffectiveRole.Staff or EffectiveRole.StaffLeader
        && user.PrimaryCampusId == partner.OwnerCampusId;
}

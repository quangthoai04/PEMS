using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Authorization for managing a still-pending account's email confirmation (resend / edit email). Only an
/// HO (any campus) or the Staff Leader of the account's OWN campus may act; anyone else — including a
/// campus-mismatched leader — is refused with 403.
///
/// <para>
/// Campus alone is not enough for a leader. A Staff Leader shares a campus with accounts they have no
/// authority over — the campus's other leader, a DEPARTMENT/STAFF, their own account — so the target's
/// role/sub-role is checked against the same three shapes the role-change command allows. This is the
/// final gate: a direct API call that never opened the modal is held to exactly these rules.
/// </para>
/// </summary>
public static class PendingAccountAuthorization
{
    public static void EnsureCanManagePending(ICurrentUserService actor, User account, string targetRoleCode)
    {
        if (actor.UserId is null || !actor.IsAuthenticated)
            throw new ForbiddenException("Bạn cần đăng nhập để thực hiện thao tác này.");

        if (actor.RoleCode == RoleCodes.Ho) return;

        var isCampusLeader = actor.RoleCode == RoleCodes.Staff
            && actor.SubRole == UserSubRoles.Leader
            && actor.PrimaryCampusId is not null
            && actor.PrimaryCampusId == account.PrimaryCampusId;

        if (!isCampusLeader)
            throw new ForbiddenException("Bạn không có quyền quản lý tài khoản này.");

        // Nobody manages their own pending account: whoever holds the address is the one who has to
        // prove it, and re-issuing your own link is not an administrative act.
        if (actor.UserId.Value == account.UserId)
            throw new ForbiddenException("Bạn không thể thực hiện thao tác này trên tài khoản của chính mình.");

        if (!IsStaffLeaderManageableShape(targetRoleCode, account.SubRole))
            throw new ForbiddenException("Tài khoản này nằm ngoài phạm vi quản lý của bạn.");
    }

    /// <summary>
    /// The three account shapes a Staff Leader manages (UC-100-SL §3.1/§3.3): an IC staff member, a
    /// department head, or a student. An HO, another Staff Leader, a VISITOR or a DEPARTMENT/STAFF is
    /// not one of them.
    /// </summary>
    public static bool IsStaffLeaderManageableShape(string roleCode, string? subRole)
        => (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
            || (roleCode == RoleCodes.Department && subRole == UserSubRoles.Leader)
            || (roleCode == RoleCodes.Student && string.IsNullOrEmpty(subRole));
}

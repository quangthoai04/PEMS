using PEMS.Domain.Constants;

namespace PEMS.Application.Common.Security;

/// <summary>
/// UC-106 shared access-eligibility rule: a DEPARTMENT-role account only keeps system access
/// while its linked department is ACTIVE. Used by credentials login, Google SSO, refresh token
/// and the per-request session validation middleware so the rule can never drift between flows.
/// Other roles (STAFF/HO/ADMIN/STUDENT/VISITOR) are never blocked by department status, even if
/// abnormal data gives them a department_id.
/// </summary>
public static class DepartmentAccessRule
{
    /// <summary>
    /// True when access must be denied: the user has role DEPARTMENT and the current
    /// department status (null when no department is linked / it no longer exists) is not ACTIVE.
    /// </summary>
    public static bool IsBlocked(string? roleCode, string? departmentStatus)
        => roleCode == RoleCodes.Department && departmentStatus != EntityStatuses.Active;

    /// <summary>User-facing Vietnamese message for the DEPARTMENT_INACTIVE error.</summary>
    public const string BlockedMessage =
        "Phòng ban của tài khoản đã ngừng hoạt động. Vui lòng liên hệ Trưởng phòng Hợp tác quốc tế tại cơ sở.";
}

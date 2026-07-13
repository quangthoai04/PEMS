using PEMS.Domain.Constants;

namespace PEMS.Application.Common.Security;

/// <summary>
/// UC-86 campus force-logout access rule (BR-AUTH-CAMPUS-06, mirror of
/// <see cref="DepartmentAccessRule"/>): a campus-scoped internal account only keeps system
/// access while its PRIMARY campus is ACTIVE. Applied by credentials login, internal Google
/// SSO, the refresh-token flow and the per-request session-validation middleware, so a user
/// whose campus was disabled loses access immediately even if a session escaped the bulk
/// revocation done at disable time (revocation never replaces this gate — BR-AUTH-CAMPUS-07).
///
/// Scope = the internal roles that belong to a campus: STAFF (Staff Leader / IC Staff),
/// DEPARTMENT (Department Leader / Staff) and STUDENT. HO and ADMIN are system-level
/// administrators and are deliberately NEVER blocked by campus status (they must stay able to
/// manage and re-enable the campus — BR-AUTH-CAMPUS-02). VISITOR is not an internal
/// campus-scoped account. The campus status must come from the DATABASE at check time — never
/// from the JWT, which may predate the disable.
/// </summary>
public static class CampusAccessRule
{
    /// <summary>
    /// True when access must be denied: the user has a campus-scoped internal role
    /// (STAFF/DEPARTMENT/STUDENT) and the current primary-campus status (null when none is
    /// linked / it no longer exists) is not ACTIVE.
    /// </summary>
    public static bool IsBlocked(string? roleCode, string? campusStatus)
        => (roleCode == RoleCodes.Staff
            || roleCode == RoleCodes.Department
            || roleCode == RoleCodes.Student)
           && campusStatus != EntityStatuses.Active;

    /// <summary>User-facing Vietnamese message for CAMPUS_INACTIVE_ACCESS_DENIED (doc §11).</summary>
    public const string BlockedMessage =
        "Cơ sở của tài khoản hiện đã ngừng hoạt động. Vui lòng liên hệ Head Office để được hỗ trợ.";
}

using PEMS.Domain.Constants;

namespace PEMS.Application.Common.Security;

/// <summary>
/// UC-86 campus-disable access rule (mirror of <see cref="DepartmentAccessRule"/>): a
/// campus-operational account only keeps system access while its PRIMARY campus is ACTIVE.
/// Applied by the per-request session-validation middleware and the refresh-token flow so a
/// user whose campus was disabled loses access immediately, even if a session escaped the
/// bulk revocation done at disable time.
///
/// Scope = the roles that operate AT a campus: STAFF (IC Staff / Staff Leader) and DEPARTMENT
/// (general-department Leader/Staff). HO and ADMIN are system-level administrators and are
/// deliberately NEVER blocked by campus status (so disabling a campus can never lock out an
/// administrator). VISITOR has no primary campus; STUDENT is out of the reception workflow.
///
/// Login is already covered separately: internal login forces the selected campus to equal the
/// user's primary campus and rejects a non-ACTIVE selected campus (CAMPUS_INACTIVE), so a user
/// of a disabled campus cannot sign back in.
/// </summary>
public static class CampusAccessRule
{
    /// <summary>
    /// True when access must be denied: the user has a campus-operational role (STAFF/DEPARTMENT)
    /// and the current primary-campus status (null when none is linked / it no longer exists) is
    /// not ACTIVE.
    /// </summary>
    public static bool IsBlocked(string? roleCode, string? campusStatus)
        => (roleCode == RoleCodes.Staff || roleCode == RoleCodes.Department)
           && campusStatus != EntityStatuses.Active;

    /// <summary>User-facing Vietnamese message for the campus-inactive access block.</summary>
    public const string BlockedMessage =
        "Cơ sở của tài khoản đã ngừng hoạt động. Vui lòng liên hệ Head Office để được hỗ trợ.";
}

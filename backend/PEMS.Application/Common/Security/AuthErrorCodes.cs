namespace PEMS.Application.Common.Security;

/// <summary>
/// Stable, machine-readable error codes returned in the <c>"errorCode"</c> field of
/// authentication failures. The frontend maps these to localized messages
/// (see <c>AUTH_ERROR_MESSAGES</c>). Values must stay in sync with the frontend map.
/// </summary>
public static class AuthErrorCodes
{
    public const string CampusRequired = "CAMPUS_REQUIRED";
    public const string CampusMismatch = "CAMPUS_MISMATCH";
    public const string CampusNotFound = "CAMPUS_NOT_FOUND";
    public const string CampusInactive = "CAMPUS_INACTIVE";
    public const string PortalMismatch = "PORTAL_MISMATCH";
    public const string InternalAccountNotFound = "INTERNAL_ACCOUNT_NOT_FOUND";
    public const string PasswordLoginDisabled = "PASSWORD_LOGIN_DISABLED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string AccountLocked = "ACCOUNT_LOCKED";

    /// <summary>
    /// P0 #1: a newly-created account whose owner has NOT yet confirmed their email. It cannot sign in
    /// (credentials or SSO) or refresh until the email is confirmed. The frontend shows a specific
    /// "chưa xác nhận email" message (never a generic inactive/campus error). HTTP 403.
    /// </summary>
    public const string AccountPendingEmailConfirmation = "ACCOUNT_PENDING_EMAIL_CONFIRMATION";

    /// <summary>
    /// UC-106: a DEPARTMENT-role account whose linked department is INACTIVE may not sign in
    /// (credentials or SSO) or refresh a token until the department is enabled again. HTTP 403.
    /// </summary>
    public const string DepartmentInactive = "DEPARTMENT_INACTIVE";

    /// <summary>
    /// UC-86 force-logout: a campus-scoped account (STAFF/DEPARTMENT/STUDENT) whose PRIMARY
    /// campus is INACTIVE may not sign in, refresh, or continue an authenticated request until
    /// the campus is enabled again. The frontend clears auth state and redirects to login on
    /// this code. HTTP 403.
    /// </summary>
    public const string CampusInactiveAccessDenied = "CAMPUS_INACTIVE_ACCESS_DENIED";
    public const string SsoDisabled = "SSO_DISABLED";

    public const string ExternalAuthFailed = "EXTERNAL_AUTH_FAILED";
    public const string VisitorProvisionDisabled = "VISITOR_PROVISION_DISABLED";

    /// <summary>Session was revoked server-side (logout, security context change). HTTP 401.</summary>
    public const string SessionRevoked = "SESSION_REVOKED";

    /// <summary>Access/refresh token expired or invalid at a protected endpoint. HTTP 401.</summary>
    public const string TokenExpired = "TOKEN_EXPIRED";

    /// <summary>Generic unauthenticated state. HTTP 401.</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>Unexpected server error. Production responses NEVER include details. HTTP 500.</summary>
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";
}

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
    /// UC-106: a DEPARTMENT-role account whose linked department is INACTIVE may not sign in
    /// (credentials or SSO) or refresh a token until the department is enabled again. HTTP 403.
    /// </summary>
    public const string DepartmentInactive = "DEPARTMENT_INACTIVE";
    public const string SsoDisabled = "SSO_DISABLED";
    public const string FeidDisabled = "FEID_DISABLED";

    /// <summary>FEID login was attempted but no real FEID provider/credential is configured yet.</summary>
    public const string FeidNotConfigured = "FEID_NOT_CONFIGURED";

    /// <summary>FEID identity is valid but not eligible (e.g. student cohort below the minimum).</summary>
    public const string FeidNotEligible = "FEID_NOT_ELIGIBLE";

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

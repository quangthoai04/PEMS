namespace PEMS.Domain.Constants;

/// <summary>
/// Canonical string values used by the authentication / RBAC tables.
/// These mirror the MySQL ENUM definitions in database/scripts/pems_full.sql.
/// Keeping them as constants avoids magic strings scattered across the codebase
/// while still letting EF map the underlying columns as plain strings.
/// </summary>
public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string Ho = "HO";
    public const string Staff = "STAFF";
    public const string Department = "DEPARTMENT";
    public const string Student = "STUDENT";
    public const string Visitor = "VISITOR";
}

public static class UserSubRoles
{
    public const string Leader = "LEADER";
    public const string Staff = "STAFF";
}

public static class RolePermissionSubRoles
{
    public const string None = "NONE";
    public const string Leader = "LEADER";
    public const string Staff = "STAFF";
}

public static class UserStatuses
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
    public const string Locked = "LOCKED";
}

public static class EntityStatuses
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

public static class ProviderTypes
{
    public const string LocalPassword = "LOCAL_PASSWORD";
    public const string GoogleSso = "GOOGLE_SSO";
    public const string FeId = "FEID";
}

public static class LoginPortals
{
    public const string Internal = "INTERNAL";
    public const string Visitor = "VISITOR";
}

public static class PermissionLevels
{
    public const string Full = "F";
    public const string Execute = "E";
    public const string Read = "R";
    public const string Own = "O";

    /// <summary>
    /// Numeric rank used for "minimum level" checks. Higher == more powerful.
    /// O (own) is intentionally ranked between Read and Execute, but callers
    /// that require O must still verify ownership of the target resource.
    /// </summary>
    public static int Rank(string? level) => level switch
    {
        Full => 4,
        Execute => 3,
        Own => 2,
        Read => 1,
        _ => 0
    };

    /// <summary>
    /// Returns true when <paramref name="actual"/> satisfies <paramref name="required"/>.
    /// </summary>
    public static bool Satisfies(string? actual, string required)
        => Rank(actual) >= Rank(required);
}

public static class LoginLogStatuses
{
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
    public const string Blocked = "BLOCKED";
}

public static class OtpPurposes
{
    public const string VisitRequestVerify = "VISIT_REQUEST_VERIFY";
    public const string ChangeSensitiveAction = "CHANGE_SENSITIVE_ACTION";
}

public static class OtpTokenTypes
{
    public const string OtpCode = "OTP_CODE";
    public const string MagicLink = "MAGIC_LINK";
}

// otp_tokens.issue_reason — why a challenge/code was issued. Recovery issues have a
// separate (stricter) hourly quota than standard INITIAL/RESEND issues.
public static class OtpIssueReasons
{
    public const string Initial = "INITIAL";
    public const string Resend = "RESEND";
    public const string HumanRecovery = "HUMAN_RECOVERY";
}

// otp_tokens.invalidation_reason values.
public static class OtpInvalidationReasons
{
    public const string MaxAttempts = "MAX_ATTEMPTS";
    public const string HumanRecovery = "HUMAN_RECOVERY";
    public const string SupersededByResend = "SUPERSEDED_BY_RESEND";
}

// Machine-readable error codes for the UC-17 OTP challenge flow. Surfaced to the client
// as response.errorCode together with remainingAttempts/retryAfterSeconds/
// humanVerificationRequired metadata (see ExceptionHandlingMiddleware) — the frontend
// must never parse the Vietnamese message.
public static class OtpErrorCodes
{
    public const string Invalid = "OTP_INVALID";
    public const string Expired = "OTP_EXPIRED";
    public const string NotFound = "OTP_NOT_FOUND";
    public const string SessionInvalid = "OTP_SESSION_INVALID";
    public const string RetryLater = "OTP_RETRY_LATER";
    public const string HumanVerificationRequired = "OTP_HUMAN_VERIFICATION_REQUIRED";
    public const string HumanVerificationFailed = "HUMAN_VERIFICATION_FAILED";
    public const string ResendRateLimited = "OTP_RESEND_RATE_LIMITED";
}

public static class SecurityEventTypes
{
    public const string SsoLogin = "SSO_LOGIN";
    public const string PortalValidation = "PORTAL_VALIDATION";
    public const string CampusValidation = "CAMPUS_VALIDATION";
    public const string VisitorAutoProvision = "VISITOR_AUTO_PROVISION";
    public const string SessionCreated = "SESSION_CREATED";
    public const string SessionRevoked = "SESSION_REVOKED";
    public const string SessionExpired = "SESSION_EXPIRED";
    public const string TokenRefresh = "TOKEN_REFRESH";
    public const string SecurityPolicyCheck = "SECURITY_POLICY_CHECK";

    /// <summary>A LOCKED Staff Leader was replaced (REPLACE_STAFF_LEADER spec §18).</summary>
    public const string StaffLeaderReplacedWhileLocked = "STAFF_LEADER_REPLACED_WHILE_LOCKED";
}

public static class SecurityEventFailureReasonCodes
{
    public const string AccountNotFound = "ACCOUNT_NOT_FOUND";
    public const string AccountDisabled = "ACCOUNT_DISABLED";
    public const string PortalMismatch = "PORTAL_MISMATCH";
    public const string CampusMismatch = "CAMPUS_MISMATCH";
    public const string RoleMismatch = "ROLE_MISMATCH";
    public const string SsoProviderError = "SSO_PROVIDER_ERROR";
    public const string InvalidSsoClaims = "INVALID_SSO_CLAIMS";
    public const string VisitorAutoProvisionDisabled = "VISITOR_AUTO_PROVISION_DISABLED";
    public const string SessionExpired = "SESSION_EXPIRED";
    public const string TokenRevoked = "TOKEN_REVOKED";
    public const string SuspiciousIp = "SUSPICIOUS_IP";
    public const string Unknown = "UNKNOWN";
}

public static class SecuritySeverities
{
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";
    public const string Critical = "CRITICAL";
}

public static class SessionRevokeReasons
{
    public const string UserLogout = "USER_LOGOUT";
    public const string PasswordReset = "PASSWORD_RESET";
    public const string PasswordChanged = "PASSWORD_CHANGED";
    public const string AccountDeactivated = "ACCOUNT_DEACTIVATED";
    public const string RoleChanged = "ROLE_CHANGED";
}

public static class CreatedViaValues
{
    public const string ManualCreated = "MANUAL_CREATED";
    public const string VisitorForm = "VISITOR_FORM";

    /// <summary>
    /// Visitor account created automatically on first external (SSO/FEID) login at the
    /// Visitor portal. Requires the <c>users.created_via</c> ENUM to include this value
    /// (see database/scripts/patch_auth_dual_portal_sso_first.sql).
    /// </summary>
    public const string SsoAutoProvision = "SSO_AUTO_PROVISION";
}

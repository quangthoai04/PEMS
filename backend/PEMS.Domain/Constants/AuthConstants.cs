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

    /// <summary>
    /// A newly-created internal account that has NOT yet proven ownership of its email address. It cannot
    /// log in (password/SSO/refresh all reject it), cannot be a Host, and holds no effective authority —
    /// a Head slot may be reserved for it, but the reservation grants nothing until the email is confirmed
    /// and the account transitions to <see cref="Active"/>. Distinct from <see cref="Inactive"/>, which
    /// means a once-active account was deactivated.
    /// </summary>
    public const string PendingEmailConfirmation = "PENDING_EMAIL_CONFIRMATION";
}

/// <summary>
/// Lifecycle of a row in <c>account_email_confirmations</c> — the one-time email-ownership proof for a
/// <see cref="UserStatuses.PendingEmailConfirmation"/> account. Only ONE row per user may be PENDING.
/// </summary>
public static class AccountEmailConfirmationStatuses
{
    /// <summary>Awaiting confirmation; the raw token is live (only its hash is stored).</summary>
    public const string Pending = "PENDING";

    /// <summary>The token was used to confirm the email; the account was activated.</summary>
    public const string Confirmed = "CONFIRMED";

    /// <summary>The confirmation window elapsed without use.</summary>
    public const string Expired = "EXPIRED";

    /// <summary>Replaced by a newer confirmation (resend / email edit); the old token no longer works.</summary>
    public const string Superseded = "SUPERSEDED";

    /// <summary>The pending account was cancelled/deleted before confirming; the reservation is released.</summary>
    public const string Cancelled = "CANCELLED";
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
    public const string ResendTooSoon = "OTP_RESEND_TOO_SOON";
    public const string StandardRateLimited = "OTP_STANDARD_RATE_LIMITED";
    public const string RecoveryRateLimited = "OTP_RECOVERY_RATE_LIMITED";
    public const string AbsoluteRateLimited = "OTP_ABSOLUTE_RATE_LIMITED";
}

/// <summary>
/// <c>security_events.event_type</c>. Only values with a real production producer exist here — the
/// legacy PORTAL_VALIDATION / CAMPUS_VALIDATION / VISITOR_AUTO_PROVISION / SESSION_CREATED /
/// SESSION_EXPIRED / TOKEN_REFRESH values were never written by any flow and were dropped from the
/// ENUM along with these constants.
/// </summary>
public static class SecurityEventTypes
{
    /// <summary>A Google SSO sign-in attempt that succeeded, or failed at 401.</summary>
    public const string SsoLogin = "SSO_LOGIN";

    /// <summary>A session was revoked (logout).</summary>
    public const string SessionRevoked = "SESSION_REVOKED";

    /// <summary>
    /// A policy decision: an attempt refused at 403 (any portal), or a successful administrative
    /// security action (campus disabled, LOCKED Staff Leader replaced).
    /// </summary>
    public const string SecurityPolicyCheck = "SECURITY_POLICY_CHECK";

    /// <summary>A LOCKED Staff Leader was replaced (REPLACE_STAFF_LEADER spec §18).</summary>
    public const string StaffLeaderReplacedWhileLocked = SecurityPolicyCheck;

    /// <summary>
    /// UC-86 force-logout: one aggregate event per campus disable, carrying the affected
    /// account count and revoked session count (never one event per session).
    /// </summary>
    public const string CampusDisabledSessionsRevoked = SecurityPolicyCheck;
}

public static class SecurityEventDetailMarkers
{
    public const string StaffLeaderReplacedWhileLocked = "STAFF_LEADER_REPLACED_WHILE_LOCKED";
    public const string CampusDisabledSessionsRevoked = "CAMPUS_DISABLED_SESSIONS_REVOKED";
}

/// <summary>
/// <c>security_events.failure_reason_code</c>. Stored as VARCHAR(80), so the set can grow with real
/// flows without a schema change and historical rows keep whatever code they were written with.
/// Only codes an actual production path emits are declared; the legacy PORTAL_MISMATCH /
/// CAMPUS_MISMATCH / ROLE_MISMATCH / SESSION_EXPIRED / TOKEN_REVOKED / SUSPICIOUS_IP / UNKNOWN
/// constants had no producer and were removed.
/// </summary>
public static class SecurityEventFailureReasonCodes
{
    public const string AccountNotFound = "ACCOUNT_NOT_FOUND";
    public const string AccountDisabled = "ACCOUNT_DISABLED";
    public const string SsoProviderError = "SSO_PROVIDER_ERROR";

    /// <summary>
    /// The presented external identity was not the one the account is bound to (Google <c>sub</c>
    /// mismatch), or the token itself failed validation. At a BLOCKED outcome this is the
    /// takeover signal that <c>SecuritySeverityResolver</c> rates CRITICAL.
    /// </summary>
    public const string InvalidSsoClaims = "INVALID_SSO_CLAIMS";

    public const string VisitorAutoProvisionDisabled = "VISITOR_AUTO_PROVISION_DISABLED";
}

/// <summary>
/// <c>security_events.severity</c>. Never chosen by a producer — see
/// <c>PEMS.Application.Common.Security.SecuritySeverityResolver</c>, the single place that maps
/// (event type, result, failure reason) onto this scale.
/// </summary>
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

    /// <summary>HO basic-info edit changed the login email, so every active session is revoked.</summary>
    public const string AccountEmailChanged = "ACCOUNT_EMAIL_CHANGED";

    /// <summary>HO basic-info edit updated identity fields (used when only non-email fields changed).</summary>
    public const string AccountBasicInfoUpdated = "ACCOUNT_BASIC_INFO_UPDATED";

    /// <summary>UC-106: the user's department was disabled, so every active session is revoked.</summary>
    public const string DepartmentDisabled = "DEPARTMENT_DISABLED";

    /// <summary>UC-86: the user's primary campus was disabled, so every active session is revoked.</summary>
    public const string CampusDisabled = "CAMPUS_DISABLED";

    /// <summary>
    /// ADMIN security-locked the account (ACTIVE → LOCKED), so every active session is revoked
    /// immediately. Kept distinct from <see cref="AccountDeactivated"/>: the two are the same
    /// mechanism but not the same event, and an operator reading <c>user_sessions.revoked_reason</c>
    /// needs to tell a security incident from a personnel change. Free-text column — no schema change.
    /// </summary>
    public const string AccountSecurityLocked = "ACCOUNT_SECURITY_LOCKED";
}

public static class CreatedViaValues
{
    public const string ManualCreated = "MANUAL_CREATED";
    public const string VisitorForm = "VISITOR_FORM";

    /// <summary>
    /// Visitor account created automatically on first external (Google SSO) login at the
    /// Visitor portal. Requires the <c>users.created_via</c> ENUM to include this value
    /// (see database/scripts/patch_auth_dual_portal_sso_first.sql).
    /// </summary>
    public const string SsoAutoProvision = "SSO_AUTO_PROVISION";
}

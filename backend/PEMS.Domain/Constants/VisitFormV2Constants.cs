namespace PEMS.Domain.Constants;

// Per-campus form v2 constants. Values MUST match the PR-2 SQL enums/columns exactly
// (docs/database/scripts/percampus_v2_migration).

public static class FormSchemaVersions
{
    public const byte Legacy = 1; // global form on visit_requests (compatibility projection)
    public const byte PerCampus = 2; // active data in visit_instance_form_details
}

public static class PrimaryContactAccessStatuses
{
    public const string PendingConfirmation = "PENDING_CONFIRMATION";
    public const string Active = "ACTIVE";
}

public static class IdentityChangeKinds
{
    public const string InitialClaim = "INITIAL_CLAIM";
    public const string Transfer = "TRANSFER";
}

public static class IdentityChangeTargetRelations
{
    public const string PrimaryContact = "PRIMARY_CONTACT";
}

public static class IdentityConfirmationMethods
{
    public const string GoogleSso = "GOOGLE_SSO";
    public const string OtpFallback = "OTP_FALLBACK";
}

public static class IdentityChangeStatuses
{
    public const string Pending = "PENDING";
    public const string Applied = "APPLIED";
    public const string Declined = "DECLINED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
    public const string Superseded = "SUPERSEDED";
}

public static class AmendmentStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Withdrawn = "WITHDRAWN";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
}

public static class FormRevisionSourceTypes
{
    public const string Create = "CREATE";
    public const string SafeEdit = "SAFE_EDIT";
    public const string AmendmentApplied = "AMENDMENT_APPLIED";
    public const string Migration = "MIGRATION";
    public const string Resubmit = "RESUBMIT";
}

// Stable error codes for per-campus form v2 read paths (surfaced as response.errorCode).
public static class VisitFormV2ErrorCodes
{
    // A v1 endpoint/DTO cannot represent a v2 request that has mixed per-campus detail.
    public const string FormVersionUpgradeRequired = "FORM_VERSION_UPGRADE_REQUIRED";

    // A v2 instance is missing its required per-campus detail (never silently falls back to global).
    public const string VisitFormDetailMissing = "VISIT_FORM_DETAIL_MISSING";

    // The actor is not allowed to view the requested campus instance scope.
    public const string VisitInstanceScopeForbidden = "VISIT_INSTANCE_SCOPE_FORBIDDEN";
}

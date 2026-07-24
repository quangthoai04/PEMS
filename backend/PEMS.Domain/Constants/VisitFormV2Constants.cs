namespace PEMS.Domain.Constants;

// Per-campus form constants. Values MUST match the canonical SQL enums/columns exactly.

/// <summary>
/// Historical form-schema numbers. The <c>form_schema_version</c> column they described no longer
/// exists on any table, and no runtime code branches on them — form content lives in
/// visit_instance_form_details for every request, with no other shape to distinguish.
///
/// Kept only because test fixtures still pass these values as an inert seed argument. Nothing here
/// selects a read or write path, and adding a branch on them would reintroduce the discriminator the
/// schema deliberately dropped.
/// </summary>
public static class FormSchemaVersions
{
    public const byte Legacy = 1;
    public const byte PerCampus = 2;
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

/// <summary>visit_instance_amendment_changes.change_class — the BACKEND field classification
/// (plan §16.6). The backend is the only source of classification; the frontend merely predicts.</summary>
public static class AmendmentChangeClasses
{
    public const string Safe = "SAFE";
    public const string PrivacyUrgent = "PRIVACY_URGENT";
    public const string ApprovalSensitive = "APPROVAL_SENSITIVE";
    public const string Structural = "STRUCTURAL";
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

    // ── Phase E — safe edit + amendments (plan §16.6, handoff §7.8) ──

    // The safe-edit endpoint received a change to a field outside the SAFE allowlist (fail closed).
    public const string SafeEditFieldNotAllowed = "SAFE_EDIT_FIELD_NOT_ALLOWED";

    // Optimistic-concurrency conflict on the safe-edit/amendment payload versions.
    public const string VisitFormConcurrencyConflict = "VISIT_FORM_CONCURRENCY_CONFLICT";

    public const string AmendmentAlreadyPending = "AMENDMENT_ALREADY_PENDING";
    public const string AmendmentNotEditable = "AMENDMENT_NOT_EDITABLE";
    public const string AmendmentBaseRevisionConflict = "AMENDMENT_BASE_REVISION_CONFLICT";
    public const string AmendmentApproverScopeForbidden = "AMENDMENT_APPROVER_SCOPE_FORBIDDEN";
    public const string AmendmentWindowExpired = "AMENDMENT_WINDOW_EXPIRED";
}

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

/// <summary>
/// How a campus's operational contact came to be linked
/// (<c>visit_request_campuses.operational_contact_confirmation_source</c>).
/// </summary>
public static class OperationalContactSources
{
    /// <summary>Contact email matched the registrant's verified email at submit: auto-linked, no invitation, no email sent.</summary>
    public const string RegistrantSelfMatch = "REGISTRANT_SELF_MATCH";
    /// <summary>The invited person accepted the per-campus confirmation link.</summary>
    public const string EmailConfirmation = "EMAIL_CONFIRMATION";
    /// <summary>Ownership handed to a new person after the campus already had a decision.</summary>
    public const string Transfer = "TRANSFER";
}

public static class IdentityChangeKinds
{
    /// <summary>First invitation for a campus that has no operational contact yet.</summary>
    public const string InitialConfirmation = "INITIAL_CONFIRMATION";
    /// <summary>Hand-over from an existing confirmed contact; the old owner keeps rights until the new one accepts.</summary>
    public const string Transfer = "TRANSFER";
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

/// <summary>
/// Why a revision row was written. Values MUST exist in the canonical SQL enums for
/// <c>visit_instance_form_revision_history.source_type</c> and
/// <c>visit_request_revision_history.source_type</c>.
/// </summary>
public static class FormRevisionSourceTypes
{
    public const string Create = "CREATE";
    /// <summary>Apply-now correction to the narrow safe field subset ("sửa nhanh").</summary>
    public const string SafeEdit = "SAFE_EDIT";
    /// <summary>
    /// Full edit of a still-pending request ("sửa đơn"). A DIFFERENT act from a safe edit: it can
    /// rewrite content, add and drop campuses, and it exists only before any campus has decided.
    /// It used to be written as SAFE_EDIT, so the timeline told users their full edit was a quick one.
    /// Added to both source_type enums by 2026_07_26_visit_pending_edit_source_type.sql.
    /// </summary>
    public const string PendingEdit = "PENDING_EDIT";
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
    public const string AmendmentNoChanges = "AMENDMENT_NO_CHANGES";
    public const string AmendmentBaseRevisionConflict = "AMENDMENT_BASE_REVISION_CONFLICT";
    public const string AmendmentApproverScopeForbidden = "AMENDMENT_APPROVER_SCOPE_FORBIDDEN";
    public const string AmendmentWindowExpired = "AMENDMENT_WINDOW_EXPIRED";
}

/// <summary>
/// Stable error codes for the per-campus operational-contact confirmation workflow (plan §5.2).
/// Public-facing ones must never reveal whether an email or account exists.
/// </summary>
public static class OperationalContactErrorCodes
{
    /// <summary>409 — an action needs the global confirmation gate open and it is not.</summary>
    public const string ContactConfirmationRequired = "CONTACT_CONFIRMATION_REQUIRED";

    public const string ConfirmationNotFound = "OPERATIONAL_CONTACT_CONFIRMATION_NOT_FOUND";
    public const string ConfirmationExpired = "OPERATIONAL_CONTACT_CONFIRMATION_EXPIRED";
    public const string ConfirmationSuperseded = "OPERATIONAL_CONTACT_CONFIRMATION_SUPERSEDED";
    /// <summary>The signed-in account's normalized email is not the invited address.</summary>
    public const string EmailMismatch = "OPERATIONAL_CONTACT_EMAIL_MISMATCH";
    public const string AlreadyConfirmed = "OPERATIONAL_CONTACT_ALREADY_CONFIRMED";
    /// <summary>429 — resend cooldown or 24h cap hit; response carries Retry-After.</summary>
    public const string RateLimited = "OPERATIONAL_CONTACT_CONFIRMATION_RATE_LIMITED";
    /// <summary>A competing identity change won the race for this campus.</summary>
    public const string ChangeConflict = "OPERATIONAL_CONTACT_CHANGE_CONFLICT";
    /// <summary>The account is not ACTIVE, so it cannot take the contact role.</summary>
    public const string AccountInactive = "OPERATIONAL_CONTACT_ACCOUNT_INACTIVE";
}

/// <summary>Scope / decision error codes shared by the campus-approval endpoints (plan §5.2).</summary>
public static class CampusScopeErrorCodes
{
    /// <summary>403 — the actor's campus is not this instance's campus.</summary>
    public const string CampusScopeForbidden = "CAMPUS_SCOPE_FORBIDDEN";
    /// <summary>The instance exists but does not belong to the request in the route.</summary>
    public const string InstanceNotInRequest = "VISIT_INSTANCE_NOT_IN_REQUEST";
    /// <summary>First valid decision already won; a later one is refused, not overwritten.</summary>
    public const string ApprovalAlreadyDecided = "APPROVAL_ALREADY_DECIDED";
    public const string HostScheduleConflict = "HOST_SCHEDULE_CONFLICT";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    /// <summary>The campus has no ACTIVE Staff Leader to route to.</summary>
    public const string StaffLeaderNotAvailable = "STAFF_LEADER_NOT_AVAILABLE";
}

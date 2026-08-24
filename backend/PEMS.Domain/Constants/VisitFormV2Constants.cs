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

    /// <summary>
    /// Same-person operational-contact metadata/relation changes applied directly by
    /// <c>VisitSafeEditService</c> (plan CanhIter3FixBug). Never routed through
    /// <c>VisitFieldClassifier</c>'s SAFE/PrivacyUrgent gate — these entries exist only so the Safe Edit
    /// response's <c>AppliedChanges</c> (and therefore <c>SubmitVisitSafeEditCommandHandler</c>'s
    /// post-commit notification targeting) can name the exact instance a contact edit touched. Never
    /// notification-worthy on its own: <c>NotifyAfterCommitAsync</c> filters to <see cref="Safe"/>/
    /// <see cref="PrivacyUrgent"/> only.
    /// </summary>
    public const string Contact = "CONTACT";
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

    /// <summary>
    /// A per-campus pending edit (<c>ApplyInstancePendingEditAsync</c>) named neither a content change
    /// nor a schedule change. Matched by CODE on the frontend, never by message text — a plain "Lưu"
    /// on this code is an informational no-op (info toast, stay on the screen), not a failed mutation;
    /// when the SAME request also asked to approve, the edit half is skipped as a no-op and the
    /// approval still goes through, so this code is never raised for that combined case.
    /// </summary>
    public const string PendingCampusNoContentChanges = "PENDING_CAMPUS_NO_CONTENT_CHANGES";

    public const string AmendmentAlreadyPending = "AMENDMENT_ALREADY_PENDING";
    public const string AmendmentNotEditable = "AMENDMENT_NOT_EDITABLE";
    public const string AmendmentNoChanges = "AMENDMENT_NO_CHANGES";
    public const string AmendmentBaseRevisionConflict = "AMENDMENT_BASE_REVISION_CONFLICT";
    public const string AmendmentApproverScopeForbidden = "AMENDMENT_APPROVER_SCOPE_FORBIDDEN";
    public const string AmendmentWindowExpired = "AMENDMENT_WINDOW_EXPIRED";

    /// <summary>
    /// An amendment tried to change the operational contact's EMAIL. Who holds a campus moves only
    /// through the contact workflow, where the new person has to accept; an amendment that could write
    /// the address directly was a second, silent way to hand the campus over.
    /// </summary>
    public const string ContactEmailNotAmendable = "CONTACT_EMAIL_NOT_AMENDABLE";

    /// <summary>
    /// An amendment tried to change the contact's NAME/ORGANIZATION/JOB TITLE/PHONE. That describes a
    /// person, and only ONE workflow may redescribe them: "Manage the contact role" (plan
    /// PEMS_CONTACT_ONE_DOOR). Quick Edit and Amendment used to offer a second and third way to write
    /// the same columns, none of them agreeing on validation or audit — this is the fail-closed guard
    /// that keeps a handcrafted request from reopening either door. A proposal may still say WHICH
    /// delegation member the contact IS (<see cref="VisitFieldClassifier.OperationalContactMemberKey"/>);
    /// it may not redescribe the contact itself.
    /// </summary>
    public const string ContactProfileNotAmendable = "CONTACT_PROFILE_NOT_AMENDABLE";
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
    /// <summary>
    /// The account answering is an INTERNAL FPTU account (ADMIN/HO/STAFF/DEPARTMENT/STUDENT), and the
    /// operational contact is external by definition — the confirmation exists so somebody outside FPTU
    /// says they are coming. Distinct from
    /// <c>CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT</c>, which answers the other half of the same
    /// rule: that one refuses an address as it is being NAMED, this one refuses an account as it tries
    /// to TAKE the role.
    /// </summary>
    public const string MustBeExternal = "OPERATIONAL_CONTACT_MUST_BE_EXTERNAL";
    /// <summary>A metadata-only save that would write exactly what is already stored.</summary>
    public const string ProfileNoChanges = "OPERATIONAL_CONTACT_PROFILE_NO_CHANGES";
    /// <summary>
    /// 422 — a TRANSFER invitation's stored snapshot has no Organization. Every write path that raises
    /// a transfer now requires Organization (Save/Replace/Transfer's own validators), but an invitation
    /// minted before that rule may still carry a blank one; accepting it is refused rather than let a
    /// legacy snapshot write a live NULL through a door the validators no longer allow.
    /// </summary>
    public const string OrganizationRequired = "OPERATIONAL_CONTACT_ORGANIZATION_REQUIRED";

    // ── Which MEMBER of the delegation the contact is (NP-03 stable identity) ──
    // Separate from the codes above, which are all about the confirmation workflow. These three are
    // about the relation itself, and each has its own way out, which is why they are not one code.

    /// <summary>
    /// The submitted member key names nobody in this campus. In practice that means the person who is
    /// the campus's contact was removed from the delegation in the same edit — refused rather than
    /// absorbed, because silently dropping the link puts the contact back to being a name to be
    /// string-matched, and silently keeping it would point at a deleted row.
    /// </summary>
    public const string MemberNotFound = "OPERATIONAL_CONTACT_MEMBER_NOT_FOUND";

    /// <summary>Two member rows arrived under one key — the client's identities are not identities.</summary>
    public const string MemberAmbiguous = "OPERATIONAL_CONTACT_MEMBER_AMBIGUOUS";

    /// <summary>
    /// The named row is not a guest-side member of this campus. The role belongs to the delegation's
    /// own side (GUEST / EXTERNAL_SUPPORT); FPTU's host and internal participants are not eligible.
    /// </summary>
    public const string MemberNotEligible = "OPERATIONAL_CONTACT_MEMBER_NOT_ELIGIBLE";

    /// <summary>
    /// The relation names a member whose FullName/JobTitle/Organization does not match the contact
    /// snapshot's (<see cref="PEMS.Shared.PersonIdentity.Key"/>-equal) — plan CanhIter3FixBug §5/§6.
    /// Distinct from <see cref="MemberNotEligible"/> (wrong TYPE of member) and
    /// <see cref="MemberNotFound"/> (no such member at all): this one names a real, eligible member of
    /// the right campus who is simply a different person than the contact snapshot describes.
    /// </summary>
    public const string RelationProfileMismatch = "OPERATIONAL_CONTACT_RELATION_PROFILE_MISMATCH";
}

/// <summary>
/// User-facing sentences for the member-identity refusals above. Kept beside the codes so the same
/// wording is raised wherever the rule is enforced — the delete guard, the create path and the edit
/// path all describe one situation and must not describe it three different ways.
/// </summary>
public static class OperationalContactMessages
{
    /// <summary>
    /// Shown when an edit removes the member who holds the role. It names the reason and the way out
    /// in one sentence, because "không tìm thấy thành viên" would leave the user re-reading a form
    /// that looks perfectly fine.
    /// </summary>
    public const string MemberNotInDelegation =
        "Người này đang là đầu mối của đoàn. Vui lòng chọn đầu mối khác trước khi thực hiện thao tác này.";

    public const string MemberAmbiguous =
        "Không xác định được đầu mối của đoàn vì có nhiều thành viên trùng định danh. Vui lòng tải lại biểu mẫu và chọn lại.";

    public const string MemberNotEligible =
        "Chỉ khách trong đoàn hoặc nhân sự hỗ trợ đi cùng đoàn mới có thể làm đầu mối.";
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

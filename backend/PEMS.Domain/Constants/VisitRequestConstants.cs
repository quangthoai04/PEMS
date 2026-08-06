namespace PEMS.Domain.Constants;

// Aggregate request status. Derived from campus-instance state; the real approve/reject decision
// lives on each campus instance, never on the request.
public static class VisitRequestStatuses
{
    /// <summary>
    /// At least one active campus has no confirmed operational contact. This is the GLOBAL GATE:
    /// while a request is here, NO Staff Leader of ANY campus may see or process it — not even a
    /// campus whose own contact is already confirmed, and not even when the registrant is that
    /// campus's own Staff Leader.
    /// </summary>
    public const string PendingContactConfirmation = "PENDING_CONTACT_CONFIRMATION";
    public const string PendingApproval          = "PENDING_APPROVAL";
    public const string PartiallyApproved        = "PARTIALLY_APPROVED";
    public const string Approved                 = "APPROVED";
    public const string Rejected                 = "REJECTED";
    public const string Cancelled                = "CANCELLED";

    /// <summary>True while the confirmation gate is shut for the whole request.</summary>
    public static bool IsBehindContactGate(string? status)
        => status == PendingContactConfirmation;
}

public static class VisitScopes
{
    public const string SingleCampus = "SINGLE_CAMPUS";
    public const string MultiCampus  = "MULTI_CAMPUS";
}

public static class WorkingLanguages
{
    public const string Vietnamese = "VI";
    public const string English    = "EN";
}

// Campus instance status (visit_request_campuses.status).
// There is no WAITING_HOST_ASSIGNMENT: approving names the Host in the same transaction and lands the
// campus on ASSIGNED. Preparation opens only after the Host starts it (ASSIGNED → BEFORE_VISIT).
public static class VisitInstanceStatuses
{
    /// <summary>This campus's operational contact has not confirmed yet; operational_contact_user_id is NULL.</summary>
    public const string WaitingContactConfirmation = "WAITING_CONTACT_CONFIRMATION";
    public const string WaitingRequestApproval = "WAITING_REQUEST_APPROVAL";
    /// <summary>Approved by this campus's Staff Leader with the Host named — preparation NOT started yet.</summary>
    public const string Assigned               = "ASSIGNED";
    /// <summary>The current Host started preparation; setup actions are open only from here.</summary>
    public const string BeforeVisit            = "BEFORE_VISIT";
    public const string DuringVisit            = "DURING_VISIT";
    public const string AfterVisit             = "AFTER_VISIT";
    public const string Closed                 = "CLOSED";
    public const string Cancelled              = "CANCELLED";
    public const string Rejected               = "REJECTED";

    /// <summary>
    /// Decided by the Staff Leader and not yet started — ASSIGNED (Host named, idle) plus BEFORE_VISIT
    /// (Host preparing). Use this for things that only need "this campus has an owner and a date":
    /// host handover, requester-side amendments, cancel-before-start, schedule conflicts.
    /// Do NOT use it to gate a setup mutation — those are BEFORE_VISIT only.
    /// </summary>
    public static readonly string[] DecidedNotStarted = { Assigned, BeforeVisit };

    /// <summary>Statuses that count as approved when aggregating the request status.</summary>
    public static readonly string[] ApprovedOrBeyond =
        { Assigned, BeforeVisit, DuringVisit, AfterVisit, Closed };

    /// <summary>Statuses still awaiting a campus decision (either gate stage).</summary>
    public static readonly string[] AwaitingDecision =
        { WaitingContactConfirmation, WaitingRequestApproval };
}

// Per-campus processing mode chosen by an AUTHENTICATED creator (visit-request create).
// Visitor/public always SEND_FOR_REVIEW; Staff may SELF_HOST their own campus; a Staff
// Leader may SELF_HOST or ASSIGN_HOST on their own campus. Backend revalidates everything.
public static class CampusSubmissionModes
{
    public const string SendForReview = "SEND_FOR_REVIEW";
    public const string SelfHost      = "SELF_HOST";
    public const string AssignHost    = "ASSIGN_HOST";
}

// Machine-readable error codes for the UC-17 public visit-request flow.
// Surfaced to the client as response.errorCode (see ExceptionHandlingMiddleware).
public static class VisitRequestErrorCodes
{
    public const string DuplicateVisitRequest = "DUPLICATE_VISIT_REQUEST";

    // Same submissionId re-used with a DIFFERENT registrant email / business fingerprint —
    // an idempotency-key replay with changed content is rejected, never silently replayed.
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string CampusNotFound        = "CAMPUS_NOT_FOUND";
    public const string CampusInactive        = "CAMPUS_INACTIVE";
    public const string InvalidVisitScope     = "INVALID_VISIT_SCOPE";
    public const string InvalidVisitTime      = "INVALID_VISIT_TIME";

    // Campus routing (campus-independent approval): every selected campus must have
    // an ACTIVE Staff Leader who will receive and process its instance.
    public const string CampusHasNoActiveStaffLeader = "CAMPUS_HAS_NO_ACTIVE_STAFF_LEADER";

    // Operational availability recheck on submit (UC-86 §11): the campus has no ACTIVE IC
    // department, or its IC-department/Staff-Leader configuration is invalid (more than one
    // active IC department / more than one valid active Staff Leader — never auto-resolved).
    public const string CampusHasNoActiveIcDepartment = "CAMPUS_HAS_NO_ACTIVE_IC_DEPARTMENT";
    public const string CampusStaffLeaderConfigurationInvalid = "CAMPUS_STAFF_LEADER_CONFIGURATION_INVALID";

    // Approve must carry the official host in the same action (no WAITING_HOST_ASSIGNMENT).
    public const string HostRequiredOnApproval = "HOST_REQUIRED_ON_APPROVAL";

    // Every campus must name the address that will be asked to run it. The column is NOT NULL and the
    // confirmation workflow has nowhere to send an invitation without it.
    public const string OperationalContactEmailRequired = "OPERATIONAL_CONTACT_EMAIL_REQUIRED";

    // ── Preparation lifecycle (ASSIGNED → BEFORE_VISIT) ──
    // A setup mutation was attempted on a campus the Host has not started preparing. Distinct from a
    // plain conflict on purpose: this one is recoverable with a single click, so the UI can offer it.
    public const string VisitPreparationNotStarted = "VISIT_PREPARATION_NOT_STARTED";
    // Start-preparation was called on a campus already in (or past) BEFORE_VISIT by someone other than
    // the actor who started it — a genuine conflict rather than an idempotent replay.
    public const string VisitPreparationAlreadyStarted = "VISIT_PREPARATION_ALREADY_STARTED";


    // contactEmail belongs to an existing non-VISITOR (internal) account — it must
    // never be repurposed as a Visitor nor have its role changed.
    public const string ContactEmailCannotBeUsedForVisitorAccount =
        "CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT";

    // contactEmail belongs to an existing VISITOR account that is not ACTIVE.
    public const string VisitorAccountInactive = "VISITOR_ACCOUNT_INACTIVE";

    // ── Visitor edit / resubmit / cancel-24h (SQL v10 resubmit_agenda_cancel24) ──
    // The request is not in an editable state (must be PENDING_APPROVAL with every campus
    // still WAITING_REQUEST_APPROVAL and ≥ 24h before the earliest start).
    public const string VisitRequestNotEditable = "VISIT_REQUEST_NOT_EDITABLE";
    // The request is not resubmittable (must be REJECTED with every campus REJECTED).
    public const string VisitRequestNotResubmittable = "VISIT_REQUEST_NOT_RESUBMITTABLE";
    // Resubmit must keep the exact same campus set (change campuses ⇒ create a new request).
    public const string ResubmitCampusListChanged = "RESUBMIT_CAMPUS_LIST_CHANGED";
    // Visitor cancel/edit blocked because a campus starts within 24 hours.
    public const string VisitCancelWindowExpired = "VISIT_CANCEL_WINDOW_EXPIRED";
    // Cancel blocked because a campus already started (DURING_VISIT / AFTER_VISIT / CLOSED).
    public const string VisitAlreadyStartedCannotCancel = "VISIT_ALREADY_STARTED_CANNOT_CANCEL";
    // Host may only cancel their campus instance BEFORE planned_start_at.
    public const string HostCannotCancelAfterVisitStarted = "HOST_CANNOT_CANCEL_AFTER_VISIT_STARTED";
    // A campus instance needs ≥ 1 agenda row before moving to DURING_VISIT / AFTER_VISIT / CLOSED.
    public const string VisitAgendaRequiredBeforeStart = "VISIT_AGENDA_REQUIRED_BEFORE_START";

    // ── Actor-relation / authenticated create (registrant vs contact owner) ──
    // Public registrant email belongs to an internal account — the public flow cannot
    // verify/provision it as a VISITOR; the user must use the internal portal instead.
    public const string RegistrantEmailBelongsToInternalAccount = "REGISTRANT_EMAIL_BELONGS_TO_INTERNAL_ACCOUNT";
    // An internal (STAFF/STAFF LEADER) registrant tried to use their own email as the
    // contact owner — internal users can never be the contact owner.
    public const string InternalRegistrantCannotBeContact = "INTERNAL_REGISTRANT_CANNOT_BE_CONTACT";
    // The caller's role may not create visit requests at all (ADMIN/HO/DEPARTMENT/STUDENT).
    public const string RoleCannotCreateVisitRequest = "ROLE_CANNOT_CREATE_VISIT_REQUEST";
    // The authenticated DIRECT create carried a registrant email that is not the caller's own.
    // The caller must re-submit through the delegated OTP flow (initiate → verify) so the named
    // registrant proves ownership of that mailbox. Nothing is written when this is raised.
    public const string RegistrantEmailVerificationRequired = "REGISTRANT_EMAIL_VERIFICATION_REQUIRED";
    // ── Reception-host arrangement (Host dự kiến) ──
    // hostSelectionMode was not one of SELF / SELECTED / WAIT_FOR_LATER, or contradicted itself
    // (WAIT_FOR_LATER carrying a proposed host).
    public const string InvalidHostSelectionMode = "INVALID_HOST_SELECTION_MODE";
    // A Visitor / external payload proposed a reception host. External submits always wait for the
    // campus Staff Leader to assign; there is no role in which they may name FPTU staff.
    public const string ProposedHostNotAllowedForRole = "PROPOSED_HOST_NOT_ALLOWED_FOR_ROLE";
    // Staff tried to propose a host for a campus other than their own primary campus.
    public const string ProposeHostOtherCampusForbidden = "PROPOSE_HOST_OTHER_CAMPUS_FORBIDDEN";
    // Regular Staff tried to propose somebody other than themself.
    public const string StaffCannotAssignOtherHost = "STAFF_CANNOT_ASSIGN_OTHER_HOST";
    // The proposed host is invalid (inactive / wrong role / other campus / not IC).
    public const string InvalidHostCandidate = "INVALID_HOST_CANDIDATE";
    // A host arrangement references a campus that is not among the selected campuses.
    public const string HostSelectionCampusNotSelected = "HOST_SELECTION_CAMPUS_NOT_SELECTED";
    // The acting Staff does not qualify to host (not ACTIVE IC staff of that campus).
    public const string SelfHostNotEligible = "SELF_HOST_NOT_ELIGIBLE";
    // A proposed-host update was attempted outside the pre-decision window, on a campus that is
    // already decided, or by somebody with no proposal rights on it.
    public const string ProposedHostNotEditable = "PROPOSED_HOST_NOT_EDITABLE";

    // ── Per-campus form edit / resubmit (plan §6.4) ──
    // VISIT_REQUEST_NOT_PER_CAMPUS_V2 lived here to reject a v1 request from the v2 edit endpoints. Pure V2
    // dropped form_schema_version, so there is no longer a request that could be rejected for that reason
    // and no code path could raise it. The schema contract test asserts the column is really gone.
    // Optimistic concurrency: the payload's expected request row_version is stale — reload and retry.
    public const string RequestVersionConflict = "VISIT_REQUEST_VERSION_CONFLICT";
    // Optimistic concurrency: an edited campus instance's expected row_version is stale.
    public const string InstanceVersionConflict = "VISIT_INSTANCE_VERSION_CONFLICT";
    // A campus removal was blocked: the instance already carries downstream data (participants/
    // agendas/logistics) or is past the removable lifecycle.
    public const string InstanceNotRemovable = "VISIT_INSTANCE_NOT_REMOVABLE";
    // An edited instance id does not belong to this request (or its campus code was changed —
    // moving an instance to another campus is remove + add, never an in-place mutation).
    public const string InstanceEditInvalid = "VISIT_INSTANCE_EDIT_INVALID";
}

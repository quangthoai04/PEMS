namespace PEMS.Domain.Constants;

// Aggregate request status (SQL v10 campus-independent approval).
// Derived from visit_request_campuses decisions; the real approve/reject decision
// lives on each campus instance, never on the request.
public static class VisitRequestStatuses
{
    public const string PendingApproval          = "PENDING_APPROVAL";
    public const string PartiallyApproved        = "PARTIALLY_APPROVED";
    public const string Approved                 = "APPROVED";
    public const string Rejected                 = "REJECTED";
    public const string Cancelled                = "CANCELLED";
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

// Campus instance status (SQL v10 visit_request_campuses.status).
// No WAITING_HOST_ASSIGNMENT: approve assigns the host in the same action.
public static class VisitInstanceStatuses
{
    public const string WaitingRequestApproval = "WAITING_REQUEST_APPROVAL";
    public const string Assigned               = "ASSIGNED";
    public const string BeforeVisit            = "BEFORE_VISIT";
    public const string DuringVisit            = "DURING_VISIT";
    public const string AfterVisit             = "AFTER_VISIT";
    public const string Closed                 = "CLOSED";
    public const string Cancelled              = "CANCELLED";
    public const string Rejected               = "REJECTED";
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
    // A Visitor (or public) payload carried SELF_HOST/ASSIGN_HOST/host metadata.
    public const string InvalidCampusSubmissionMode = "INVALID_CAMPUS_SUBMISSION_MODE";
    // Staff tried to direct-process a campus other than their own primary campus.
    public const string DirectProcessOtherCampusForbidden = "DIRECT_PROCESS_OTHER_CAMPUS_FORBIDDEN";
    // Regular Staff tried to assign a host other than themself.
    public const string StaffCannotAssignOtherHost = "STAFF_CANNOT_ASSIGN_OTHER_HOST";
    // ASSIGN_HOST candidate is invalid (inactive / wrong role / other campus / not IC).
    public const string InvalidHostCandidate = "INVALID_HOST_CANDIDATE";
    // Direct mode payload references a campus that is not among the selected campuses.
    public const string DirectModeCampusNotSelected = "DIRECT_MODE_CAMPUS_NOT_SELECTED";
    // The acting Staff does not qualify for direct self-host (not ACTIVE IC staff of that campus).
    public const string SelfHostNotEligible = "SELF_HOST_NOT_ELIGIBLE";
}

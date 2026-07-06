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

// Machine-readable error codes for the UC-17 public visit-request flow.
// Surfaced to the client as response.errorCode (see ExceptionHandlingMiddleware).
public static class VisitRequestErrorCodes
{
    public const string DuplicateVisitRequest = "DUPLICATE_VISIT_REQUEST";
    public const string CampusNotFound        = "CAMPUS_NOT_FOUND";
    public const string CampusInactive        = "CAMPUS_INACTIVE";
    public const string InvalidVisitScope     = "INVALID_VISIT_SCOPE";
    public const string InvalidVisitTime      = "INVALID_VISIT_TIME";

    // Campus routing (campus-independent approval): every selected campus must have
    // an ACTIVE Staff Leader who will receive and process its instance.
    public const string CampusHasNoActiveStaffLeader = "CAMPUS_HAS_NO_ACTIVE_STAFF_LEADER";

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
}

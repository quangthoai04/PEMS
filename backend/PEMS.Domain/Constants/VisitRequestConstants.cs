namespace PEMS.Domain.Constants;

// Request decision status only (SQL v8.3 visit_requests.status).
// Visit progress (IN_PROGRESS/COMPLETED/etc.) is derived from visit_request_campuses.status
// and must NOT be stored on the request.
public static class VisitRequestStatuses
{
    public const string PendingApproval          = "PENDING_APPROVAL";
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
    public const string Other      = "OTHER";
}

// Campus instance progress status (SQL v8.3 visit_request_campuses.status).
public static class VisitInstanceStatuses
{
    public const string WaitingRequestApproval = "WAITING_REQUEST_APPROVAL";
    public const string Assigned               = "ASSIGNED";
    public const string BeforeVisit            = "BEFORE_VISIT";
    public const string DuringVisit            = "DURING_VISIT";
    public const string AfterVisit             = "AFTER_VISIT";
    public const string Closed                 = "CLOSED";
    public const string Cancelled              = "CANCELLED";
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

    // contactEmail belongs to an existing non-VISITOR (internal) account — it must
    // never be repurposed as a Visitor nor have its role changed.
    public const string ContactEmailCannotBeUsedForVisitorAccount =
        "CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT";

    // contactEmail belongs to an existing VISITOR account that is not ACTIVE.
    public const string VisitorAccountInactive = "VISITOR_ACCOUNT_INACTIVE";
}

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

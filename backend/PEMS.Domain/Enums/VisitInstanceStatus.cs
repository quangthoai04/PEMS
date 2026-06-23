namespace PEMS.Shared;

public static class VisitInstanceStatus
{
    public const string WaitingRequestApproval = "WAITING_REQUEST_APPROVAL";
    public const string WaitingHostAssignment = "WAITING_HOST_ASSIGNMENT";
    public const string Assigned = "ASSIGNED";
    public const string BeforeVisit = "BEFORE_VISIT";
    public const string DuringVisit = "DURING_VISIT";
    public const string AfterVisit = "AFTER_VISIT";
    public const string Closed = "CLOSED";
    public const string Cancelled = "CANCELLED";
}

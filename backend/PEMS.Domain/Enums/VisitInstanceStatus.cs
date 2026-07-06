namespace PEMS.Shared;

// Campus instance status (SQL v10 campus-independent approval).
// WAITING_HOST_ASSIGNMENT was removed: approve always assigns the host in the same action.
// REJECTED was added: each Staff Leader rejects their own campus instance independently.
public static class VisitInstanceStatus
{
    public const string WaitingRequestApproval = "WAITING_REQUEST_APPROVAL";
    public const string Assigned = "ASSIGNED";
    public const string BeforeVisit = "BEFORE_VISIT";
    public const string DuringVisit = "DURING_VISIT";
    public const string AfterVisit = "AFTER_VISIT";
    public const string Closed = "CLOSED";
    public const string Cancelled = "CANCELLED";
    public const string Rejected = "REJECTED";
}

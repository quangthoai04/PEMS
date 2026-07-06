namespace PEMS.Shared;

// Aggregate request status (SQL v10 campus-independent approval).
// Derived from visit_request_campuses decisions; never a decision store itself.
public static class VisitRequestStatus
{
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string PartiallyApproved = "PARTIALLY_APPROVED";
    public const string Rejected = "REJECTED";
    public const string Approved = "APPROVED";
    public const string Cancelled = "CANCELLED";
}

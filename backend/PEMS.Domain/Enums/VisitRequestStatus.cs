namespace PEMS.Shared;

// Aggregate request status. Derived from campus-instance state; never a decision store itself.
//
// PENDING_CONTACT_CONFIRMATION is the global gate: while a request sits here, no Staff Leader of
// any campus may see or act on it, regardless of whether that particular campus already has its
// operational contact confirmed.
//
// NOTE: PEMS.Domain.Constants.VisitRequestStatuses carries the same values. Both are kept in sync
// by hand; do not add a member to one without the other.
public static class VisitRequestStatus
{
    public const string PendingContactConfirmation = "PENDING_CONTACT_CONFIRMATION";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string PartiallyApproved = "PARTIALLY_APPROVED";
    public const string Rejected = "REJECTED";
    public const string Approved = "APPROVED";
    public const string Cancelled = "CANCELLED";
}

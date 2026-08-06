namespace PEMS.Shared;

// Campus instance status (visit_request_campuses.status).
//
// WAITING_CONTACT_CONFIRMATION is the first stop: the campus has no confirmed operational contact
// yet. REJECTED stays per campus: each Staff Leader decides their own instance independently.
//
// ASSIGNED and BEFORE_VISIT are DIFFERENT states with different actors, and the boundary between
// them is the point of this enum:
//
//   ASSIGNED     — the Staff Leader approved and named the Host in one transaction. The campus has a
//                  current_host_user_id and a decision, and NOTHING about the visit may be set up
//                  yet. The Host has been given the job; they have not picked it up.
//   BEFORE_VISIT — the Host explicitly started preparation (StartVisitPreparationCommand). Only here
//                  do agenda, participants, logistics, reminders, the prep note and the setup-progress
//                  email open up.
//
// The move between them is a deliberate business action by the CURRENT HOST, never a side effect:
// opening a screen, loading a detail, previewing an email or saving some unrelated field must never
// advance the campus. Approve is likewise forbidden from writing BEFORE_VISIT directly.
//
// visit_participants.status and visit_logistics_items.status also carry a value called ASSIGNED. That
// is a different enum meaning "this person/task was assigned", and is unrelated to this one.
//
// NOTE: PEMS.Domain.Constants.VisitInstanceStatuses carries the same values. Both are kept in sync
// by hand; do not add a member to one without the other.
public static class VisitInstanceStatus
{
    public const string WaitingContactConfirmation = "WAITING_CONTACT_CONFIRMATION";
    public const string WaitingRequestApproval = "WAITING_REQUEST_APPROVAL";
    /// <summary>Approved by the campus Staff Leader with the Host named; preparation NOT started yet.</summary>
    public const string Assigned = "ASSIGNED";
    /// <summary>The current Host started preparation; every setup action is open from here.</summary>
    public const string BeforeVisit = "BEFORE_VISIT";
    public const string DuringVisit = "DURING_VISIT";
    public const string AfterVisit = "AFTER_VISIT";
    public const string Closed = "CLOSED";
    public const string Cancelled = "CANCELLED";
    public const string Rejected = "REJECTED";
}

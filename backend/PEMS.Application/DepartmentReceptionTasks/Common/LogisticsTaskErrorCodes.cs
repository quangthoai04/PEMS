namespace PEMS.Application.DepartmentReceptionTasks.Common;

/// <summary>
/// Stable error codes for the department reception-task flows (assigning a logistics request and
/// proposing a change to one).
///
/// <para>
/// These exist because both handlers used to signal every business refusal with a bare
/// <see cref="System.Exception"/>, which <c>ExceptionHandlingMiddleware</c> can only treat as an
/// unhandled fault: HTTP 500 <c>INTERNAL_SERVER_ERROR</c> with the message replaced by "Đã xảy ra lỗi
/// hệ thống". Two things went wrong with that. The caller was told the server had broken when in fact
/// it had correctly refused — "this person is already booked at that hour" is an answer, not a crash —
/// and a genuine defect looked exactly like a routine refusal in the logs, so neither could be found
/// by watching for the other.
/// </para>
/// <para>
/// A code is the part a client may branch on; the message is for a human and may be reworded freely.
/// </para>
/// </summary>
public static class LogisticsTaskErrorCodes
{
    // ── Assigning a request to a department staff member ──

    /// <summary>The request belongs to a different department than the caller's.</summary>
    public const string AssignmentOutOfDepartmentScope = "LOGISTICS_ASSIGNMENT_OUT_OF_SCOPE";

    /// <summary>The request's status does not admit a (re)assignment — it is already in flight or closed.</summary>
    public const string AssignmentStatusNotAssignable = "LOGISTICS_ASSIGNMENT_STATUS_NOT_ASSIGNABLE";

    /// <summary>An assignment attempt is still PENDING; the previous assignee has not answered yet.</summary>
    public const string AssignmentAlreadyPending = "LOGISTICS_ASSIGNMENT_ALREADY_PENDING";

    /// <summary>A handover has been signed, so the responsible person can no longer be swapped.</summary>
    public const string AssignmentHandoverSigned = "LOGISTICS_ASSIGNMENT_HANDOVER_SIGNED";

    /// <summary>The chosen person is not an active member of the caller's department.</summary>
    public const string AssigneeNotEligible = "LOGISTICS_ASSIGNEE_NOT_ELIGIBLE";

    /// <summary>
    /// The chosen person already has a commitment overlapping this request's usage window. The most
    /// common refusal of the lot, and the one whose 500 was least defensible: a scheduling clash is
    /// ordinary, expected, and the operator's next move is simply to pick somebody else.
    /// </summary>
    public const string AssigneeScheduleConflict = "LOGISTICS_ASSIGNEE_SCHEDULE_CONFLICT";

    // ── Proposing a change to a request ──

    /// <summary>The mandatory rationale for the proposal is missing.</summary>
    public const string ProposalNoteRequired = "LOGISTICS_PROPOSAL_NOTE_REQUIRED";

    /// <summary>Proposed quantity is below 1, or not lower than the quantity originally requested.</summary>
    public const string ProposalQuantityInvalid = "LOGISTICS_PROPOSAL_QUANTITY_INVALID";

    /// <summary>The proposed usage window ends at or before it starts.</summary>
    public const string ProposalWindowInvalid = "LOGISTICS_PROPOSAL_WINDOW_INVALID";

    /// <summary>The proposer's department does not own this request.</summary>
    public const string ProposalOutOfDepartmentScope = "LOGISTICS_PROPOSAL_OUT_OF_SCOPE";

    /// <summary>A department staff member may only propose on a request assigned to them.</summary>
    public const string ProposalNotAssignedToProposer = "LOGISTICS_PROPOSAL_NOT_ASSIGNED_TO_PROPOSER";

    // ── Shared ──

    /// <summary>The logistics request does not exist.</summary>
    public const string RequestNotFound = "LOGISTICS_REQUEST_NOT_FOUND";
}

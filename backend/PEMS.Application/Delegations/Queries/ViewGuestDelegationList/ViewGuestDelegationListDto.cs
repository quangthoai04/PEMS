using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

public sealed class VisitRequestManagementItemDto
{
    public ulong VisitRequestId { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public string? RequestCode { get; set; }
    public string? DelegationName { get; set; }
    public string? PartnerName { get; set; }

    public string RequestStatus { get; set; } = default!;
    public string? CampusStatus { get; set; }

    public string? VisitScope { get; set; }

    public ulong? CampusId { get; set; }
    public string? CampusName { get; set; }
    /// <summary>Number of campus instances on the request (used for the "Liên cơ sở (N)" badge on request-level rows).</summary>
    public int CampusCount { get; set; }

    public ulong? CreatedByUserId { get; set; }
    public ulong? CurrentHostUserId { get; set; }
    public string? HostName { get; set; }
    /// <summary>How the current host was assigned (AUTO_STAFF_LEADER / MANUAL_APPROVAL / TRANSFERRED).</summary>
    public string? HostAssignmentSource { get; set; }
    /// <summary>True when the signed-in user is the current host of this campus instance.</summary>
    public bool CurrentUserIsHost { get; set; }

    public ulong? VisitorUserId { get; set; }
    public string? VisitorName { get; set; }

    public bool IsCurrentUserParticipant { get; set; }
    /// <summary>The signed-in user's participation role on the attending tab (IC_SUPPORT/DEPT_SUPPORT/...).</summary>
    public string? ParticipantRole { get; set; }

    /// <summary>Which tab this row belongs to: RESPONSIBLE | INVITED | MY_REQUESTS | TASKS.</summary>
    public string TabType { get; set; } = "RESPONSIBLE";

    /// <summary>
    /// The signed-in user's relation to this row. One of:
    /// NONE | HO_APPROVER | HO_MONITOR | CAMPUS_APPROVER | TEMP_HOST | HOST | IC_SUPPORT |
    /// DEPT_SUPPORT | STUDENT_SUPPORT | VISITOR_OWNER | DEPARTMENT_TASK_OWNER.
    /// HO_APPROVER = HO on a pending multi-campus request (can decide); HO_MONITOR = HO
    /// read-only (single-campus, or an already-decided multi-campus request).
    /// </summary>
    public string CurrentUserRelation { get; set; } = "NONE";

    /// <summary>
    /// True when the caller may only view this row (no mutating action available) — e.g.
    /// HO monitoring a SINGLE_CAMPUS request, or any attending-tab row. Convenience flag
    /// derived from <see cref="AllowedActions"/>; the frontend may use it to render a read-only badge.
    /// </summary>
    public bool IsReadOnly { get; set; }

    public DateTime? ExpectedStartAt { get; set; }
    public DateTime? ExpectedEndAt { get; set; }
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
    public int? ExpectedGuestCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancellationActorType { get; set; }
    public string? CancellationSource { get; set; }
    public ulong? CancelledBy { get; set; }
    public string? DisplayStatusLabel { get; set; }
    public string? DisplayProgressLabel { get; set; }
    /// <summary>Reason/note recorded when the request was approved or rejected (visit_requests.decision_note).</summary>
    public string? DecisionNote { get; set; }

    /// <summary>
    /// Business actions the signed-in user may take on this row, computed by the backend
    /// (single source of truth). The frontend renders buttons from this list; every action
    /// is still re-validated server-side. Possible values: VIEW_DETAIL, HO_APPROVE, HO_REJECT,
    /// APPROVE_AND_ASSIGN_HOST, CAMPUS_REJECT, TRANSFER_HOST, CANCEL_BY_VISITOR, CANCEL_BY_HOST.
    /// </summary>
    public List<string> AllowedActions { get; set; } = new();
}

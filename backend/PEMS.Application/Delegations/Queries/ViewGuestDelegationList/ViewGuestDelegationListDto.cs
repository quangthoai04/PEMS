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

    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }

    // ── Cancellation info (UC-136). Instance-level is preferred (a Host usually owns one
    // campus instance); falls back to request-level when the whole request was cancelled. ──
    /// <summary>True when the request or this campus instance is cancelled.</summary>
    public bool IsCancelled { get; set; }
    /// <summary>"REQUEST" (whole request) or "CAMPUS_INSTANCE" (this campus only); null if not cancelled.</summary>
    public string? CancellationLevel { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancellationActorType { get; set; }
    public string? CancellationSource { get; set; }
    public ulong? CancelledBy { get; set; }
    public string? CancelledByName { get; set; }
    public string? DisplayStatusLabel { get; set; }
    public string? DisplayProgressLabel { get; set; }

    /// <summary>
    /// True when this row has at least one campus instance currently in a cancellable state:
    /// the request is APPROVED and an instance is in WAITING_HOST_ASSIGNMENT / ASSIGNED /
    /// BEFORE_VISIT and has not started yet. Precomputed by the backend so the frontend never
    /// has to infer cancel-eligibility from a multi-campus summary row (which has no single
    /// instance status). Drives whether CANCEL_BY_VISITOR is offered.
    /// </summary>
    public bool HasCancellableInstance { get; set; }

    // ── Decision info (UC-18/UC-22). Reject reason = decision_note, NEVER cancellation_reason.
    // Surfaced on the list so the "Xem lý do từ chối" popup can show full metadata (who/when/role)
    // without a second fetch. Only meaningful when RequestStatus = REJECTED (or APPROVED). ──
    /// <summary>Reason/note recorded when the request was approved or rejected (visit_requests.decision_note).</summary>
    public string? DecisionNote { get; set; }
    /// <summary>User id of who decided (approved/rejected) the request (visit_requests.decided_by).</summary>
    public ulong? DecidedBy { get; set; }
    /// <summary>Display name of the deciding user.</summary>
    public string? DecidedByName { get; set; }
    /// <summary>When the request was decided (visit_requests.decided_at).</summary>
    public DateTime? DecidedAt { get; set; }
    /// <summary>Role the decision was made under: HO | STAFF_LEADER (visit_requests.decision_actor_role).</summary>
    public string? DecisionActorRole { get; set; }

    /// <summary>
    /// Business actions the signed-in user may take on this row, computed by the backend
    /// (single source of truth). The frontend renders buttons from this list; every action
    /// is still re-validated server-side. Possible values: VIEW_DETAIL, HO_APPROVE, HO_REJECT,
    /// APPROVE_AND_ASSIGN_HOST, CAMPUS_REJECT, TRANSFER_HOST, CANCEL_BY_VISITOR, CANCEL_BY_HOST.
    /// </summary>
    public List<string> AllowedActions { get; set; } = new();
}

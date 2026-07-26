using System;
using System.Collections.Generic;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;

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

    /// <summary>True when the request stores different content per campus (drives the "Khác nhau theo cơ sở" label).</summary>
    public bool HasMixedCampusDetails { get; set; }

    public ulong? CampusId { get; set; }
    public string? CampusName { get; set; }
    /// <summary>Number of campus instances on the request (used for the "Liên cơ sở (N)" badge on request-level rows).</summary>
    public int CampusCount { get; set; }

    public ulong? CreatedByUserId { get; set; }
    public ulong? CurrentHostUserId { get; set; }
    public string? HostName { get; set; }

    /// <summary>
    /// Optimistic-concurrency token of the CAMPUS INSTANCE this row is about (null on a multi-campus
    /// summary row, which is not a single instance). Carried so an instance-scoped action started from
    /// the list — the Host handover — can echo it back and get a clean 409 instead of clobbering a
    /// change somebody else made while the row was on screen.
    /// </summary>
    public int? RowVersion { get; set; }

    /// <summary>True when the signed-in user is the current host of this campus instance.</summary>
    public bool CurrentUserIsHost { get; set; }

    // ── Actor relation (registrant vs contact owner) ──
    /// <summary>Registrant (người đăng ký) account id — read-only tracking relation only.</summary>
    public ulong? RegistrantUserId { get; set; }
    /// <summary>
    /// On the REGISTERED tab: true when the registrant is ALSO the official host of at least
    /// one campus instance ("Đồng thời là host" badge). Host actions still live on the
    /// responsible/hosted tab, never here.
    /// </summary>
    public bool IsAlsoHost { get; set; }
    /// <summary>First hosted instance id when <see cref="IsAlsoHost"/> — link target to the host context.</summary>
    public ulong? AlsoHostVisitInstanceId { get; set; }

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

    // ── The three layers a row has to keep apart ──────────────────────────────────────────────
    // Where the request IS (status), what the reader is TO it (relation), and what they should DO
    // next (next task) are three different questions. They used to be answered by one status badge,
    // which is why "Chờ xử lý tại cơ sở" had to double as an instruction and told the wrong half of
    // the audience to do nothing.
    /// <summary>Process status in Vietnamese, resolved from the campus status (or the request aggregate for request-level rows).</summary>
    public string? StatusLabel { get; set; }
    /// <summary>What the signed-in user is to this row ("Bạn phụ trách tiếp đón", "Chỉ theo dõi", …).</summary>
    public string? RelationLabel { get; set; }
    /// <summary>What this particular reader should do next. Never derived from status alone — see <see cref="VisitNextTaskDto"/>.</summary>
    public VisitNextTaskDto? NextTask { get; set; }

    /// <summary>
    /// True when this row has at least one campus instance currently in a cancellable state:
    /// the request is APPROVED and an instance is in WAITING_HOST_ASSIGNMENT / ASSIGNED /
    /// BEFORE_VISIT and has not started yet. Precomputed by the backend so the frontend never
    /// has to infer cancel-eligibility from a multi-campus summary row (which has no single
    /// instance status). Drives whether CANCEL_BY_VISITOR is offered.
    /// </summary>
    public bool HasCancellableInstance { get; set; }
    public bool HasStartedCampus { get; set; }

    // ── Visitor edit / resubmit (SQL v10 resubmit_agenda_cancel24) ──
    /// <summary>Times the Visitor resubmitted this request after a full rejection.</summary>
    public int ResubmissionCount { get; set; }
    public DateTime? LastResubmittedAt { get; set; }
    public ulong? LastResubmittedBy { get; set; }
    public string? LastResubmittedByName { get; set; }
    /// <summary>
    /// True when the request is still fully pending (PENDING_APPROVAL, every campus
    /// WAITING_REQUEST_APPROVAL) and the earliest start is ≥ 24h away — the Visitor owner
    /// may edit it. Drives EDIT_PENDING_REQUEST.
    /// </summary>
    public bool CanEditPending { get; set; }
    /// <summary>
    /// True when the whole request was rejected (request REJECTED + every campus REJECTED) —
    /// the Visitor owner may edit &amp; resubmit it. Drives RESUBMIT_REJECTED_REQUEST.
    /// </summary>
    public bool CanResubmit { get; set; }

    // ── Multi-campus expandable row (Phương án A). Only meaningful on request-level rows
    // (Visitor / HO): the per-campus progress used by the accordion + backend-computed
    // action booleans so the frontend never gates on status text. ──
    /// <summary>True when this row can show the per-campus progress accordion (request has &gt; 1 campus).</summary>
    public bool CanExpandCampuses { get; set; }
    /// <summary>True when the caller may open the overall request detail.</summary>
    public bool CanViewRequestDetail { get; set; }
    /// <summary>True when the row is REJECTED and a decision note exists (show "Xem lý do từ chối").</summary>
    public bool CanViewRejectReason { get; set; }
    /// <summary>True when the request/an instance is cancelled (show "Xem lý do hủy").</summary>
    public bool CanViewCancelReason { get; set; }
    /// <summary>Per-campus progress rows for the expandable accordion. Empty for single-campus / instance-level rows.</summary>
    public List<CampusProgressItemDto> CampusProgressItems { get; set; } = new();

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
    /// APPROVE_AND_ASSIGN_HOST, CAMPUS_REJECT, CANCEL_BY_VISITOR, CANCEL_BY_HOST, TRANSFER_HOST.
    /// Kept as the ENABLED subset of <see cref="Capabilities"/> for the actions that have a verdict,
    /// so a button can never appear that the capability list says is refused.
    /// </summary>
    public List<string> AllowedActions { get; set; } = new();

    /// <summary>
    /// Mutation actions WITH their verdict — including refused ones, which carry why and until when.
    /// This is what lets the list render a disabled "Chuyển người phụ trách" with a real reason instead
    /// of hiding it and leaving the Staff Leader to guess. Same shape the detail screen uses.
    /// </summary>
    public List<VisitActionCapabilityDto> Capabilities { get; set; } = new();

    /// <summary>
    /// Where the search keyword matched, computed ONLY over the campuses this caller is authorized to see
    /// (a hidden campus never produces a context and never affects the row's hit/count/order). Null/empty
    /// when there is no keyword. Each context is either REQUEST-level (shared fields) or CAMPUS-level (one
    /// authorized instance). Carries stable field CODES, never raw snippets or PII.
    /// </summary>
    public List<SearchMatchContextDto>? MatchedContexts { get; set; }

    /// <summary>
    /// What has happened on this row since the caller last looked. Null when there is nothing to
    /// report, so a quiet row carries no payload at all.
    /// </summary>
    public VisitListChangeSummaryDto? ChangeSummary { get; set; }
}

/// <summary>
/// What this reader should do next on this row, decided by the backend.
///
/// Deliberately NOT a second name for the status: a campus sitting at ASSIGNED means "finish the
/// preparation" to its Host, "nothing to do" to the visitor, and "nothing to do" to the Staff Leader
/// who approved it. The status is one fact; the task is per-reader, so it cannot be derived on the
/// client from the status alone without getting two of those three audiences wrong.
///
/// Emptiness is a real answer: <see cref="VisitNextTaskCodes.None"/> with requiresAction=false says
/// "không có nhiệm vụ cần xử lý", which is more useful than an absent field the UI has to interpret.
/// </summary>
public sealed class VisitNextTaskDto
{
    /// <summary>One of <see cref="VisitNextTaskCodes"/>. Match on this, never on the label.</summary>
    public string Code { get; set; } = VisitNextTaskCodes.None;
    public string Label { get; set; } = "";
    /// <summary>True when this reader is the one being waited on. False for a task shown as context.</summary>
    public bool RequiresAction { get; set; }
    /// <summary>REQUEST or INSTANCE — which thing the task is about.</summary>
    public string Scope { get; set; } = VisitActionScopes.Request;
    public ulong? VisitInstanceId { get; set; }
    /// <summary>When it stops being useful to do this (planned start for pre-visit work, planned end after).</summary>
    public DateTime? DueAt { get; set; }
    /// <summary>The action in <see cref="VisitRequestManagementItemDto.AllowedActions"/> that performs it, when there is one.</summary>
    public string? ActionCode { get; set; }
    /// <summary>Set when the task is real but the action is currently refused (e.g. past the cutoff).</summary>
    public string? DisabledReason { get; set; }
}

/// <summary>The closed vocabulary of next tasks. A code the client has not been taught renders as its label.</summary>
public static class VisitNextTaskCodes
{
    public const string ReviewAndAssign = "REVIEW_AND_ASSIGN";
    public const string CompletePreparation = "COMPLETE_PREPARATION";
    public const string ConfirmPreparation = "CONFIRM_PREPARATION";
    public const string ReviewAmendment = "REVIEW_AMENDMENT";
    public const string AcceptHostHandover = "ACCEPT_HOST_HANDOVER";
    public const string RunReception = "RUN_RECEPTION";
    public const string CompletePostVisit = "COMPLETE_POST_VISIT";
    public const string CloseVisit = "CLOSE_VISIT";
    public const string None = "NONE";
}

/// <summary>
/// The "something moved here" signal for a list row — kept SEPARATE from the row's status.
///
/// A request that is still "Chờ xử lý" after the visitor rewrote it is still, correctly, chờ xử lý;
/// overwriting the status to say "Đã sửa" would lose the thing the Staff Leader is actually sorting
/// by. So this is a second, additive signal: the status says where the request IS, this says what
/// happened to it recently and whether the reader has seen it.
/// </summary>
public sealed class VisitListChangeSummaryDto
{
    public bool HasUnreadChanges { get; set; }
    public int UnreadChangeCount { get; set; }
    /// <summary>Event code of the most recent change — drives which badge the row shows.</summary>
    public string? LatestEventCode { get; set; }
    public DateTime? LatestChangedAt { get; set; }
    /// <summary>Amendments awaiting a decision on campuses this caller can see.</summary>
    public int PendingAmendmentCount { get; set; }
    /// <summary>
    /// True when at least one unread change is waiting on THIS person specifically — a Staff Leader
    /// with an amendment to approve, not a Host being told the notes changed.
    /// </summary>
    public bool RequiresViewerAction { get; set; }
    /// <summary>
    /// Per-campus indicators, restricted to the campuses the caller may see. A campus outside their
    /// scope contributes nothing here and nothing to the counts above — an unread badge must never
    /// become a way to learn that a hidden campus exists.
    /// </summary>
    public List<VisitListCampusIndicatorDto> CampusIndicators { get; set; } = new();
}

public sealed class VisitListCampusIndicatorDto
{
    public ulong VisitInstanceId { get; set; }
    public string? CampusName { get; set; }
    public string EventCode { get; set; } = default!;
    public DateTime OccurredAt { get; set; }
    public bool RequiresAction { get; set; }
}

/// <summary>
/// One place a search keyword matched. <see cref="Scope"/> is REQUEST (request-wide field such as the
/// request code) or CAMPUS (a specific authorized instance). <see cref="MatchedFields"/> are stable codes
/// from <see cref="VisitSearchFieldCodes"/> — the frontend maps them to VI/EN labels. No raw values.
/// </summary>
public sealed class SearchMatchContextDto
{
    /// <summary>REQUEST or CAMPUS (see <see cref="SearchMatchScopes"/>).</summary>
    public string Scope { get; set; } = default!;
    /// <summary>Set for CAMPUS scope only — the authorized instance the match belongs to.</summary>
    public ulong? VisitInstanceId { get; set; }
    public ulong? CampusId { get; set; }
    public string? CampusName { get; set; }
    /// <summary>Stable field codes (e.g. DELEGATION_NAME, CAMPUS). Never raw matched text.</summary>
    public List<string> MatchedFields { get; set; } = new();
}

/// <summary>Scope discriminator for <see cref="SearchMatchContextDto"/>.</summary>
public static class SearchMatchScopes
{
    public const string Request = "REQUEST";
    public const string Campus = "CAMPUS";
}

/// <summary>
/// Stable, PII-free field codes for search match contexts — the ALLOWLIST of what the list/search actually
/// searches (NOT an aspirational set). Guest/support member names are deliberately absent: they are not
/// searched by default and never produce a match context.
/// </summary>
public static class VisitSearchFieldCodes
{
    public const string RequestCode = "REQUEST_CODE";
    public const string RegistrantOrganization = "REGISTRANT_ORGANIZATION";
    public const string Partner = "PARTNER";
    public const string PrimaryContact = "PRIMARY_CONTACT";
    public const string DelegationName = "DELEGATION_NAME";
    public const string Campus = "CAMPUS";
    public const string Host = "HOST";
}

/// <summary>
/// One campus instance of a (multi-campus) request, used by the expandable-row accordion on
/// the management list. Action visibility is backend-computed (booleans) so the frontend never
/// gates on status text. Visitor-safe: carries no internal logistics/participant data.
/// </summary>
public sealed class CampusProgressItemDto
{
    public ulong VisitInstanceId { get; set; }
    public ulong CampusId { get; set; }
    public string? CampusCode { get; set; }
    public string? CampusName { get; set; }

    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }

    /// <summary>Raw status code (WAITING_REQUEST_APPROVAL/.../CANCELLED); the frontend maps it to a label.</summary>
    public string InstanceStatus { get; set; } = default!;

    public ulong? HostUserId { get; set; }
    public string? HostName { get; set; }

    /// <summary>Optimistic-concurrency token of this campus instance — echoed back by an instance-scoped action.</summary>
    public int RowVersion { get; set; }

    // ── Campus-level decision (campus-independent approval): who approved/rejected this
    // instance and why. REJECTED always carries a DecisionNote (mandatory reason). ──
    public string? DecisionNote { get; set; }
    public ulong? DecidedBy { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }

    public string? CancellationReason { get; set; }
    public ulong? CancelledBy { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationActorType { get; set; }
    public string? CancellationSource { get; set; }

    /// <summary>Always true for in-scope rows (the list is already scoped server-side).</summary>
    public bool CanViewCampusDetail { get; set; }
    /// <summary>True only for the Visitor owner when the request is APPROVED/PARTIALLY_APPROVED and this instance is still cancellable.</summary>
    public bool CanCancelCampusVisit { get; set; }
    /// <summary>True when this instance is CANCELLED (show "Xem lý do hủy").</summary>
    public bool CanViewCancelReason { get; set; }
    /// <summary>True when this instance is REJECTED and a decision note exists (show "Xem lý do từ chối").</summary>
    public bool CanViewRejectReason { get; set; }

    /// <summary>
    /// Per-campus mutation verdicts. A multi-campus request has no single Host, so an action that
    /// picks one — handing over the Host role above all — belongs HERE and never on the aggregate row:
    /// a "Chuyển người phụ trách" on the summary row would have to guess which campus it meant.
    /// Refused entries are included with their reason so the campus can show a disabled action.
    /// </summary>
    public List<VisitActionCapabilityDto> Capabilities { get; set; } = new();

    /// <summary>Convenience mirror of the enabled TRANSFER_HOST verdict for this campus.</summary>
    public bool CanTransferHost { get; set; }
}

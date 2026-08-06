using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_request_campuses")]
public class VisitRequestCampus
{
    [Key]
    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("campus_id")]
    public ulong CampusId { get; set; }


    [Column("planned_start_at")]
    public DateTime PlannedStartAt { get; set; }

    [Column("planned_end_at")]
    public DateTime PlannedEndAt { get; set; }

    // NOTE: actual_start_at / actual_end_at were removed in SQL v8.3.
    // WAITING_CONTACT_CONFIRMATION -> (WAITING_REQUEST_APPROVAL | ASSIGNED) -> BEFORE_VISIT -> ...
    // Two ways to reach ASSIGNED, both strictly after the confirmation gate opens: the campus Staff
    // Leader approves and names a host, or a proposed host authorized earlier is revalidated and
    // activated. ASSIGNED -> BEFORE_VISIT is always the Host pressing "Bắt đầu chuẩn bị".
    [Column("status")]
    public string Status { get; set; } = "WAITING_CONTACT_CONFIRMATION";

    // --- Operational contact: the confirmed account allowed to operate THIS campus. ---
    // NULL exactly while Status is WAITING_CONTACT_CONFIRMATION. Set either at submit time when the
    // contact email matches the registrant's verified email (REGISTRANT_SELF_MATCH — no invitation,
    // no email), or when the invited person accepts the per-campus confirmation link.
    //
    // Deliberately NOT unique: one person may hold several campuses, including several campuses of
    // the same request. Identity is decided ONLY by this column and the registrant relation — never
    // by the contact name or phone snapshot, and never by an email stored on the form.
    [Column("operational_contact_user_id")]
    public ulong? OperationalContactUserId { get; set; }

    [Column("operational_contact_confirmed_at")]
    public DateTime? OperationalContactConfirmedAt { get; set; }

    // REGISTRANT_SELF_MATCH | EMAIL_CONFIRMATION | TRANSFER (see OperationalContactSources).
    [Column("operational_contact_confirmation_source")]
    public string? OperationalContactConfirmationSource { get; set; }

    // --- Current Host: the OFFICIAL reception host. Never set before the confirmation gate opens. ---
    [Column("current_host_user_id")]
    public ulong? CurrentHostUserId { get; set; }

    // --- Proposed host (Host dự kiến). An INTENTION recorded at create/edit time, not an
    // assignment. It lives in its own columns precisely so that reading CurrentHostUserId can never
    // return somebody nobody has confirmed yet: the whole point of the gate is that an outside
    // contact accepts the visit before FPTU staff are told they are running it.
    //
    // SELF | SELECTED | WAIT_FOR_LATER (see HostSelectionModes). WAIT_FOR_LATER is the default and
    // the only value an external/Visitor submit can produce.
    [Column("host_selection_mode")]
    public string HostSelectionMode { get; set; } = "WAIT_FOR_LATER";

    [Column("proposed_host_user_id")]
    public ulong? ProposedHostUserId { get; set; }

    /// <summary>Who authorized the proposal. Becomes <c>DecidedBy</c> if and when it is activated.</summary>
    [Column("proposed_host_by_user_id")]
    public ulong? ProposedHostByUserId { get; set; }

    [Column("proposed_host_at")]
    public DateTime? ProposedHostAt { get; set; }

    // PENDING | ACTIVATED | NEEDS_RESELECTION (see ProposedHostActivationStatuses). NULL iff there
    // is no proposal.
    [Column("proposed_host_activation_status")]
    public string? ProposedHostActivationStatus { get; set; }

    [Column("proposed_host_activated_at")]
    public DateTime? ProposedHostActivatedAt { get; set; }

    [Column("coordinator_user_id")]
    public ulong? CoordinatorUserId { get; set; }

    [Column("coordinator_assigned_by")]
    public ulong? CoordinatorAssignedBy { get; set; }

    [Column("coordinator_assigned_at")]
    public DateTime? CoordinatorAssignedAt { get; set; }

    // --- Host assignment (set by the campus Staff Leader in the same approve action). ---
    [Column("host_assigned_by")]
    public ulong? HostAssignedBy { get; set; }

    [Column("host_assigned_at")]
    public DateTime? HostAssignedAt { get; set; }

    // --- Campus-level decision (SQL v10 campus-independent approval). Normally the Staff
    // Leader of this campus approves/rejects (decision_actor_role STAFF_LEADER); the single
    // exception is decision_actor_role STAFF + decision_source INTERNAL_SELF_HOST: a regular
    // IC Staff processed their OWN campus directly inside the create transaction of a request
    // they registered. REJECTED always requires a decision_note (DB triggers enforce all). ---
    [Column("decided_by")]
    public ulong? DecidedBy { get; set; }

    [Column("decided_at")]
    public DateTime? DecidedAt { get; set; }

    [Column("decision_actor_role")]
    public string? DecisionActorRole { get; set; }

    // STANDARD_CAMPUS_REVIEW | INTERNAL_SELF_HOST | INTERNAL_LEADER_ASSIGN (see DecisionSources).
    [Column("decision_source")]
    public string? DecisionSource { get; set; }

    [Column("decision_note")]
    public string? DecisionNote { get; set; }

    [Column("closed_by")]
    public ulong? ClosedBy { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("close_note")]
    public string? CloseNote { get; set; }

    // --- §10 đóng đoàn: Host xác nhận chuyến này KHÔNG cần bài tin tức. Điều kiện đóng đoàn về
    // tin tức = có ít nhất 1 news PUBLISHED của instance HOẶC cờ này = true. ---
    [Column("news_not_required")]
    public bool NewsNotRequired { get; set; }

    // --- Cancellation (UC-136). cancellation_reason carries both reason and external-confirmation details. ---
    [Column("cancelled_by")]
    public ulong? CancelledBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancellation_actor_type")]
    public string? CancellationActorType { get; set; }

    [Column("cancellation_source")]
    public string? CancellationSource { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    // --- Host's internal preparation note (PEMS v10 2026-06-26). Free text shown on the
    // VisitProcess "Ghi chú chung". Who/when last edited is traced via audit_logs, not extra columns. ---
    [Column("preparation_note")]
    public string? PreparationNote { get; set; }

    [Column("row_version")]
    public int RowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
    public virtual Users.User? OperationalContact { get; set; }
    public virtual Users.User? ProposedHost { get; set; }
    public virtual ICollection<VisitAgenda> Agendas { get; set; } = new List<VisitAgenda>();
    public virtual ICollection<VisitParticipant> Participants { get; set; } = new List<VisitParticipant>();
    public virtual ICollection<VisitLogisticsItem> LogisticsItems { get; set; } = new List<VisitLogisticsItem>();

    // Per-campus form v2: one-to-one form detail, per-campus guest links, amendments and history.
    public virtual VisitInstanceFormDetail? FormDetail { get; set; }
    public virtual ICollection<VisitInstanceGuestMember> GuestMemberLinks { get; set; } = new List<VisitInstanceGuestMember>();
    public virtual ICollection<VisitInstanceAmendment> Amendments { get; set; } = new List<VisitInstanceAmendment>();
    public virtual ICollection<VisitInstanceFormRevisionHistory> FormRevisionHistory { get; set; } = new List<VisitInstanceFormRevisionHistory>();

    // Per-campus operational-contact confirmation / transfer invitations.
    public virtual ICollection<VisitRequestIdentityChange> IdentityChanges { get; set; } = new List<VisitRequestIdentityChange>();
}

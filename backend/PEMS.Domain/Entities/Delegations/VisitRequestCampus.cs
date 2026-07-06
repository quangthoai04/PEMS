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
    [Column("status")]
    public string Status { get; set; } = "WAITING_REQUEST_APPROVAL";

    [Column("current_host_user_id")]
    public ulong? CurrentHostUserId { get; set; }

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

    // --- Campus-level decision (SQL v10 campus-independent approval). The Staff Leader of
    // this campus approves/rejects the instance; decision_actor_role is always STAFF_LEADER
    // and REJECTED requires a decision_note (both enforced by DB triggers as well). ---
    [Column("decided_by")]
    public ulong? DecidedBy { get; set; }

    [Column("decided_at")]
    public DateTime? DecidedAt { get; set; }

    [Column("decision_actor_role")]
    public string? DecisionActorRole { get; set; }

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
    public virtual ICollection<VisitAgenda> Agendas { get; set; } = new List<VisitAgenda>();
    public virtual ICollection<VisitParticipant> Participants { get; set; } = new List<VisitParticipant>();
    public virtual ICollection<VisitLogisticsItem> LogisticsItems { get; set; } = new List<VisitLogisticsItem>();
}

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

    [Column("instance_code")]
    public string? InstanceCode { get; set; }

    [Column("planned_start_at")]
    public DateTime PlannedStartAt { get; set; }

    [Column("planned_end_at")]
    public DateTime PlannedEndAt { get; set; }

    // NOTE: actual_start_at / actual_end_at were removed in SQL v8.3.
    // Real timing history is tracked via visit_status_logs, not on this row.

    [Column("status")]
    public string Status { get; set; } = "WAITING_REQUEST_APPROVAL";

    [Column("current_host_user_id")]
    public ulong? CurrentHostUserId { get; set; }

    // --- Host assignment (set when the overall request is approved). ---
    [Column("host_assigned_by")]
    public ulong? HostAssignedBy { get; set; }

    [Column("host_assigned_at")]
    public DateTime? HostAssignedAt { get; set; }

    [Column("host_assignment_source")]
    public string? HostAssignmentSource { get; set; }

    // --- Host transfer (Transfer Host feature). ---
    [Column("host_transferred_by")]
    public ulong? HostTransferredBy { get; set; }

    [Column("host_transferred_at")]
    public DateTime? HostTransferredAt { get; set; }

    [Column("host_transfer_note")]
    public string? HostTransferNote { get; set; }

    [Column("closed_by")]
    public ulong? ClosedBy { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("close_note")]
    public string? CloseNote { get; set; }

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
    public virtual ICollection<VisitStatusLog> StatusLogs { get; set; } = new List<VisitStatusLog>();
}

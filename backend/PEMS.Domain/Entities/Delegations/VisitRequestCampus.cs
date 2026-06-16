using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_request_campuses")]
public class VisitRequestCampus
{
    [Key]
    [Column("visit_instance_id")]
    public string VisitInstanceId { get; set; } = null!;

    [Column("visit_request_id")]
    public string VisitRequestId { get; set; } = null!;

    [Column("campus_id")]
    public string CampusId { get; set; } = null!;

    [Column("instance_code")]
    public string? InstanceCode { get; set; }

    [Column("planned_start_at")]
    public DateTime PlannedStartAt { get; set; }

    [Column("planned_end_at")]
    public DateTime PlannedEndAt { get; set; }

    [Column("actual_start_at")]
    public DateTime? ActualStartAt { get; set; }

    [Column("actual_end_at")]
    public DateTime? ActualEndAt { get; set; }

    [Column("status")]
    public string Status { get; set; } = "WAITING_REQUEST_APPROVAL";

    [Column("current_host_user_id")]
    public string? CurrentHostUserId { get; set; }

    [Column("host_transferred_by")]
    public string? HostTransferredBy { get; set; }

    [Column("host_transferred_at")]
    public DateTime? HostTransferredAt { get; set; }

    [Column("host_transfer_note")]
    public string? HostTransferNote { get; set; }

    [Column("closed_by")]
    public string? ClosedBy { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("close_note")]
    public string? CloseNote { get; set; }

    [Column("row_version")]
    public int RowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
    public virtual ICollection<VisitAgenda> Agendas { get; set; } = new List<VisitAgenda>();
    public virtual ICollection<VisitParticipant> Participants { get; set; } = new List<VisitParticipant>();
    public virtual ICollection<VisitLogisticsItem> LogisticsItems { get; set; } = new List<VisitLogisticsItem>();
    public virtual ICollection<VisitStatusLog> StatusLogs { get; set; } = new List<VisitStatusLog>();
}

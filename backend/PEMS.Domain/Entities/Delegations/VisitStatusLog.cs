using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_status_logs")]
public class VisitStatusLog
{
    [Key]
    [Column("visit_status_log_id")]
    public ulong VisitStatusLogId { get; set; }

    [Column("visit_request_id")]
    public ulong? VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong? VisitInstanceId { get; set; }

    // REQUEST = visit_requests.status; CAMPUS_INSTANCE = visit_request_campuses.status.
    [Column("status_owner_type")]
    public string StatusOwnerType { get; set; } = "CAMPUS_INSTANCE";

    [Column("old_status")]
    public string? OldStatus { get; set; }

    [Column("new_status")]
    public string NewStatus { get; set; } = null!;

    [Column("changed_by")]
    public ulong? ChangedBy { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; }

    public virtual VisitRequest? VisitRequest { get; set; }
    public virtual VisitRequestCampus? VisitInstance { get; set; }
}

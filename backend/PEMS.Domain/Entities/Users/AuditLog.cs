using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("audit_logs")]
public class AuditLog
{
    [Key]
    [Column("audit_log_id")]
    public ulong AuditLogId { get; set; }

    [Column("actor_user_id")]
    public ulong? ActorUserId { get; set; }

    [Column("campus_id")]
    public ulong? CampusId { get; set; }

    [Column("action")]
    public string Action { get; set; } = null!;

    [Column("entity_type")]
    public string EntityType { get; set; } = null!;

    [Column("entity_id")]
    public ulong? EntityId { get; set; }


    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("request_id")]
    public string? RequestId { get; set; }

    // Per-campus form v2 audit context (additive). visit_request_id / visit_instance_id carry NO
    // FK — audit must survive a business row being deleted, so these are plain nullable columns.
    [Column("correlation_id")]
    public string? CorrelationId { get; set; }

    [Column("visit_request_id")]
    public ulong? VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong? VisitInstanceId { get; set; }

    [Column("source_type")]
    public string? SourceType { get; set; }

    [Column("source_id")]
    public ulong? SourceId { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual User? ActorUser { get; set; }
    public virtual ICollection<AuditLogChange> Changes { get; set; } = new List<AuditLogChange>();
}

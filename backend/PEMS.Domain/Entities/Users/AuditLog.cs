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

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual User? ActorUser { get; set; }
    public virtual ICollection<AuditLogChange> Changes { get; set; } = new List<AuditLogChange>();
}

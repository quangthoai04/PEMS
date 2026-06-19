using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("security_events")]
public class SecurityEvent
{
    [Key]
    [Column("security_event_id")]
    public ulong SecurityEventId { get; set; }

    [Column("user_id")]
    public ulong? UserId { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("event_type")]
    public string EventType { get; set; } = null!;

    [Column("severity")]
    public string Severity { get; set; } = "LOW";

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("metadata")]
    public string? Metadata { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}

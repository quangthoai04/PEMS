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

    [Column("email_snapshot")]
    public string? EmailSnapshot { get; set; }

    [Column("event_type")]
    public string EventType { get; set; } = null!;

    [Column("result")]
    public string Result { get; set; } = null!;

    [Column("failure_reason_code")]
    public string? FailureReasonCode { get; set; }

    [Column("severity")]
    public string Severity { get; set; } = "LOW";

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("login_portal")]
    public string? LoginPortal { get; set; }

    [Column("selected_campus_id")]
    public ulong? SelectedCampusId { get; set; }

    [Column("provider_type")]
    public string? ProviderType { get; set; }

    [Column("session_id")]
    public ulong? SessionId { get; set; }

    [Column("detail_text")]
    public string? DetailText { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}

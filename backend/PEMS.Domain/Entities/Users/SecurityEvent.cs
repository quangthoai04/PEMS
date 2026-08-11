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

    /// <summary>
    /// Machine-readable reason (see <c>SecurityEventFailureReasonCodes</c>); NULL on success.
    /// Stored as VARCHAR rather than an ENUM so the set can grow with real flows and historical
    /// rows keep whatever code they were written with.
    /// </summary>
    [Column("failure_reason_code")]
    public string? FailureReasonCode { get; set; }

    /// <summary>
    /// LOW / MEDIUM / HIGH / CRITICAL. Never set by a producer — derived centrally by
    /// <c>SecuritySeverityResolver</c> inside <c>ISecurityAuditService.WriteSecurityEventAsync</c>.
    /// </summary>
    [Column("severity")]
    public string Severity { get; set; } = "LOW";

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("login_portal")]
    public string? LoginPortal { get; set; }

    /// <summary>
    /// Short debug note. Campus-scoped events carry their campus id here (<c>campusId=…</c>) —
    /// there is no dedicated campus column on this table.
    /// </summary>
    [Column("detail_text")]
    public string? DetailText { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}

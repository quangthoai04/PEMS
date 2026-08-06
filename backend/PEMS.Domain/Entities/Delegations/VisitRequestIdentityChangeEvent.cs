using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// Append-only log of every identity-change transition (created / applied / declined / expired /
/// cancelled / superseded / resent / redacted). Never updated or deleted outside the retention job.
/// </summary>
[Table("visit_request_identity_change_events")]
public class VisitRequestIdentityChangeEvent
{
    [Key]
    [Column("identity_change_event_id")]
    public ulong IdentityChangeEventId { get; set; }

    [Column("identity_change_id")]
    public ulong IdentityChangeId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    /// <summary>Campus the event belongs to, so audit always resolves back to the right site.</summary>
    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("event_type")]
    public string EventType { get; set; } = null!;

    [Column("from_status")]
    public string? FromStatus { get; set; }

    [Column("to_status")]
    public string? ToStatus { get; set; }

    [Column("actor_user_id")]
    public ulong? ActorUserId { get; set; }

    [Column("email_masked")]
    public string? EmailMasked { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("correlation_id")]
    public string? CorrelationId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual VisitRequestIdentityChange IdentityChange { get; set; } = null!;
}

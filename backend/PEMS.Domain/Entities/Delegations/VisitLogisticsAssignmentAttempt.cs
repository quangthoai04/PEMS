using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_logistics_assignment_attempts")]
public class VisitLogisticsAssignmentAttempt
{
    [Key]
    [Column("assignment_attempt_id")]
    public ulong AssignmentAttemptId { get; set; }

    [Column("logistics_item_id")]
    public ulong LogisticsItemId { get; set; }

    [Column("assignee_user_id")]
    public ulong AssigneeUserId { get; set; }

    [Column("assigned_by")]
    public ulong AssignedBy { get; set; }

    // Always set explicitly by handlers with the shared Vietnam wall-clock "now"
    // (no clock default here — Domain must not decide the time policy).
    [Column("assigned_at")]
    public DateTime AssignedAt { get; set; }

    /// <summary>PENDING | ACCEPTED | DECLINED | CANCELLED</summary>
    [Column("status")]
    public string Status { get; set; } = "PENDING";

    [Column("responded_at")]
    public DateTime? RespondedAt { get; set; }

    [Column("response_note")]
    public string? ResponseNote { get; set; }

    /// <summary>PORTAL | EMAIL_TOKEN | SYSTEM</summary>
    [Column("response_source")]
    public string? ResponseSource { get; set; }

    [Column("cancelled_by")]
    public ulong? CancelledBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public virtual VisitLogisticsItem LogisticsItem { get; set; } = null!;
}

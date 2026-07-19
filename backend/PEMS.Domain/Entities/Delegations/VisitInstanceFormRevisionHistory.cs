using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// Immutable per-instance revision history (per-campus form v2). One row per applied form_revision;
/// <c>snapshot_json</c> is the full detail snapshot AFTER that revision. Unique per
/// <c>(visit_instance_id, form_revision)</c>. Composite FK carries the request id.
/// </summary>
[Table("visit_instance_form_revision_history")]
public class VisitInstanceFormRevisionHistory
{
    [Key]
    [Column("revision_history_id")]
    public ulong RevisionHistoryId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("form_revision")]
    public uint FormRevision { get; set; }

    [Column("approval_revision")]
    public uint ApprovalRevision { get; set; }

    [Column("source_type")]
    public string SourceType { get; set; } = null!; // CREATE|SAFE_EDIT|AMENDMENT_APPLIED|MIGRATION|RESUBMIT

    [Column("source_id")]
    public ulong? SourceId { get; set; }

    [Column("snapshot_json")]
    public string SnapshotJson { get; set; } = null!;

    [Column("applied_by")]
    public ulong? AppliedBy { get; set; }

    [Column("applied_at")]
    public DateTime AppliedAt { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;
}

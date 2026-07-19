using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// Request-level snapshot history for the registrant + primary-contact DISPLAY fields (per-campus
/// form v2). Email/account relation history is kept by identity-change events, not here. Unique per
/// <c>(visit_request_id, request_revision)</c>.
/// </summary>
[Table("visit_request_revision_history")]
public class VisitRequestRevisionHistory
{
    [Key]
    [Column("request_revision_history_id")]
    public ulong RequestRevisionHistoryId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("request_revision")]
    public uint RequestRevision { get; set; }

    [Column("source_type")]
    public string SourceType { get; set; } = null!; // CREATE|SAFE_EDIT|MIGRATION|RESUBMIT

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

    public virtual VisitRequest VisitRequest { get; set; } = null!;
}

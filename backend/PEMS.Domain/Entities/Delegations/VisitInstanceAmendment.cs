using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// An approval-sensitive change proposal for an already-approved campus instance (per-campus form v2).
/// Composite FK <c>(visit_request_id, visit_instance_id)</c> ties it to the right request/instance.
/// The DB has a VIRTUAL <c>amendment_pending_guard</c> unique index (one PENDING_APPROVAL per instance)
/// that is intentionally NOT mapped here — EF never reads or writes it. PR-3 only maps/reads these rows;
/// the submit/approve/reject handlers arrive in PR-6.
/// </summary>
[Table("visit_instance_amendments")]
public class VisitInstanceAmendment
{
    [Key]
    [Column("amendment_id")]
    public ulong AmendmentId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("amendment_no")]
    public uint AmendmentNo { get; set; }

    [Column("status")]
    public string Status { get; set; } = "DRAFT"; // DRAFT|PENDING_APPROVAL|APPROVED|REJECTED|WITHDRAWN|EXPIRED|CANCELLED

    [Column("base_form_revision")]
    public uint BaseFormRevision { get; set; }

    [Column("base_approval_revision")]
    public uint BaseApprovalRevision { get; set; }

    [Column("requested_by")]
    public ulong RequestedBy { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("decided_by")]
    public ulong? DecidedBy { get; set; }

    [Column("decided_at")]
    public DateTime? DecidedAt { get; set; }

    [Column("decision_note")]
    public string? DecisionNote { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("withdrawn_at")]
    public DateTime? WithdrawnAt { get; set; }

    [Column("expected_instance_row_version")]
    public uint ExpectedInstanceRowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;
    public virtual ICollection<VisitInstanceAmendmentChange> Changes { get; set; }
        = new List<VisitInstanceAmendmentChange>();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// A per-campus form v2 submission that has been VALIDATED and OTP-challenged but not yet created
/// (public flow). The full canonical v2 snapshot is bound here at <c>initiate</c> so <c>verify</c> can
/// build the request from EXACTLY what was OTP-verified — the client cannot swap campus/member/contact/
/// time/content between initiate and verify. One row per submission intent (unique <see cref="SubmissionId"/>),
/// created at initiate, reused across OTP resends, and marked <see cref="ConsumedAt"/> when the request is
/// created. Only the SHA-256 fingerprint is used for the defence-in-depth compare; the snapshot JSON is the
/// authoritative source and is never logged.
/// </summary>
[Table("visit_request_pending_forms")]
public class VisitRequestPendingForm
{
    [Key]
    [Column("pending_form_id")]
    public ulong PendingFormId { get; set; }

    /// <summary>UUID of the submit intent — the idempotency key shared with the OTP challenge.</summary>
    [Column("submission_id")]
    public string SubmissionId { get; set; } = null!;

    /// <summary>Normalized (trim + lowercase) registrant email the OTP challenge is bound to.</summary>
    [Column("registrant_email")]
    public string RegistrantEmail { get; set; } = null!;

    [Column("form_schema_version")]
    public short FormSchemaVersion { get; set; }

    /// <summary>v2 canonical fingerprint (<c>VisitRequestFingerprintBuilder.BuildV2</c>) for the defence-in-depth
    /// verify-time compare. A mismatch means the client changed core identity after initiate.</summary>
    [Column("fingerprint_v2")]
    public string FingerprintV2 { get; set; } = null!;

    /// <summary>The full canonical <c>VisitRequestFormDataV2</c> as JSON — the request is built from THIS at verify.</summary>
    [Column("snapshot_json")]
    public string SnapshotJson { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set (in the create transaction) when the request is created from this snapshot — a consumed
    /// snapshot can never build a second request.</summary>
    [Column("consumed_at")]
    public DateTime? ConsumedAt { get; set; }
}

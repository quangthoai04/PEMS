using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// Current state of ONE campus's operational-contact INITIAL_CONFIRMATION / TRANSFER. This is the
/// state row, NOT a token store — the single-use hashed acceptance token lives in
/// <c>email_action_tokens</c>, and raw Google id tokens / OTPs are never stored anywhere.
///
/// Scope is a campus, not a request: a person invited to three campuses gets three rows. The DB
/// enforces that with a composite FK to (visit_request_id, visit_instance_id), so a payload naming
/// a sibling campus cannot produce a valid row, plus a VIRTUAL <c>pending_guard</c> unique index
/// (at most one in-flight PENDING per instance) that is intentionally NOT mapped here.
/// </summary>
[Table("visit_request_identity_changes")]
public class VisitRequestIdentityChange
{
    [Key]
    [Column("identity_change_id")]
    public ulong IdentityChangeId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    /// <summary>The campus this invitation belongs to. Never null — there is no request-wide invitation.</summary>
    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("change_kind")]
    public string ChangeKind { get; set; } = null!; // INITIAL_CONFIRMATION | TRANSFER

    /// <summary>
    /// Bumped on every resend. Feeds the dispatcher dedupe key
    /// <c>OP_CONTACT_CONFIRM:{identityChangeId}:{tokenVersion}</c> and lets a resend supersede the
    /// previous token inside the same transaction.
    /// </summary>
    [Column("token_version")]
    public uint TokenVersion { get; set; } = 1;

    [Column("confirmation_method")]
    public string ConfirmationMethod { get; set; } = "GOOGLE_SSO";

    [Column("old_user_id")]
    public ulong? OldUserId { get; set; }

    [Column("new_user_id")]
    public ulong? NewUserId { get; set; }

    [Column("old_email_normalized")]
    public string? OldEmailNormalized { get; set; }

    // Required while PENDING; may be NULL after 90-day retention redaction. new_email_masked is always kept.
    [Column("new_email_normalized")]
    public string? NewEmailNormalized { get; set; }

    [Column("new_email_masked")]
    public string NewEmailMasked { get; set; } = null!;

    [Column("pending_snapshot_json")]
    public string? PendingSnapshotJson { get; set; }

    [Column("status")]
    public string Status { get; set; } = "PENDING"; // PENDING|APPLIED|DECLINED|EXPIRED|CANCELLED|SUPERSEDED

    [Column("expected_request_row_version")]
    public uint ExpectedRequestRowVersion { get; set; }

    [Column("requested_by")]
    public ulong RequestedBy { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("applied_at")]
    public DateTime? AppliedAt { get; set; }

    [Column("declined_at")]
    public DateTime? DeclinedAt { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("superseded_at")]
    public DateTime? SupersededAt { get; set; }

    [Column("retention_until")]
    public DateTime? RetentionUntil { get; set; }

    [Column("redacted_at")]
    public DateTime? RedactedAt { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("resend_count")]
    public uint ResendCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public virtual VisitRequestCampus CampusInstance { get; set; } = null!;

    public virtual ICollection<VisitRequestIdentityChangeEvent> Events { get; set; }
        = new List<VisitRequestIdentityChangeEvent>();
}

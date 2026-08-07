using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// One full, independent form snapshot PER campus instance.
/// The ONLY source of truth for a request's delegation name, visit type, purpose, working content,
/// operational contact, language, media consent and notes — <see cref="VisitRequest"/> holds none of
/// them, so a missing row here has nothing to fall back to and is a consistency error.
/// One-to-one with <see cref="VisitRequestCampus"/> (shared PK <c>visit_instance_id</c>).
/// </summary>
[Table("visit_instance_form_details")]
public class VisitInstanceFormDetail
{
    // PK == FK to visit_request_campuses.visit_instance_id (ON DELETE CASCADE).
    [Key]
    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("delegation_name")]
    public string DelegationName { get; set; } = null!;

    [Column("visit_type")]
    public string VisitType { get; set; } = "CAMPUS_TOUR";

    [Column("visit_type_other")]
    public string? VisitTypeOther { get; set; }

    [Column("purpose")]
    public string Purpose { get; set; } = null!;

    [Column("working_content")]
    public string? WorkingContent { get; set; }

    // Per-campus OPERATIONAL contact SNAPSHOT. Name and phone are display data only: a matching
    // name or phone is never evidence of the same person. The account that actually operates this
    // campus is VisitRequestCampus.OperationalContactUserId, set only through confirmation.
    [Column("operational_contact_full_name")]
    public string OperationalContactFullName { get; set; } = null!;

    // Organization is optional. Stored NULL when blank — the DB CHECK (TRIM(x) <> '') accepts NULL
    // but rejects an empty string.
    [Column("operational_contact_organization")]
    public string? OperationalContactOrganization { get; set; }

    // REQUIRED, like name and phone. It reads as decoration next to them, but it is what tells a
    // campus whether the person on the other end of the phone can settle a schedule or has to go
    // and ask — and every detail screen already reserves a row for it.
    [Column("operational_contact_job_title")]
    public string OperationalContactJobTitle { get; set; } = null!;

    // Optional, like organization. The column is nullable and its CHECK accepts NULL but rejects an
    // empty string, so blank is normalized to NULL on the way in.
    [Column("operational_contact_phone")]
    public string? OperationalContactPhone { get; set; }

    // REQUIRED. The only address a per-campus confirmation invitation is ever bound to. Normalized
    // at the application boundary. Runtime authority is read from OperationalContactUserId, never
    // from this string.
    [Column("operational_contact_email")]
    public string OperationalContactEmail { get; set; } = null!;

    [Column("working_language")]
    public string WorkingLanguage { get; set; } = "EN";

    [Column("transportation_note")]
    public string? TransportationNote { get; set; }

    [Column("media_consent_status")]
    public string MediaConsentStatus { get; set; } = "DECLINED";

    /// <summary>
    /// The guest's one general remark to FPTU about THIS campus ("Ghi chú gửi FPTU") — dietary needs,
    /// accessibility, timing, documents. Independent of <see cref="MediaConsentStatus"/>: it is not a
    /// justification for the consent answer, and either value is valid with or without a note.
    /// </summary>
    [Column("notes")]
    public string? Notes { get; set; }

    // form_revision bumps on every applied change; approval_revision only when an
    // approval-sensitive amendment is applied. row_version is the manual optimistic token.
    [Column("form_revision")]
    public uint FormRevision { get; set; } = 1;

    [Column("approval_revision")]
    public uint ApprovalRevision { get; set; } = 1;

    [Column("row_version")]
    public int RowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;
}

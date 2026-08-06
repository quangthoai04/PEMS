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

    // Optional, like organization: the detail screens show the contact's job title, so it needs a
    // home. Blank normalizes to NULL (the CHECK rejects an empty string).
    [Column("operational_contact_job_title")]
    public string? OperationalContactJobTitle { get; set; }

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

    [Column("media_consent_note")]
    public string? MediaConsentNote { get; set; }

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

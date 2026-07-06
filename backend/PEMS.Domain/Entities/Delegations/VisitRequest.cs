using PEMS.Domain.Entities.Partners;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_requests")]
public class VisitRequest
{
    [Key]
    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("request_code")]
    public string RequestCode { get; set; } = null!;

    [Column("visitor_user_id")]
    public ulong? VisitorUserId { get; set; }

    [Column("partner_id")]
    public ulong? PartnerId { get; set; }

    [Column("created_source")]
    public string CreatedSource { get; set; } = "VISITOR_SUBMITTED";
    [Column("registrant_full_name")]
    public string RegistrantFullName { get; set; } = null!;

    [Column("registrant_nationality")]
    public string RegistrantNationality { get; set; } = null!;

    [Column("registrant_organization")]
    public string RegistrantOrganization { get; set; } = null!;

    [Column("registrant_job_title")]
    public string RegistrantJobTitle { get; set; } = null!;

    [Column("registrant_phone")]
    public string RegistrantPhone { get; set; } = null!;

    [Column("registrant_email")]
    public string RegistrantEmail { get; set; } = null!;

    [Column("delegation_name")]
    public string DelegationName { get; set; } = null!;

    [Column("visit_scope")]
    public string VisitScope { get; set; } = "SINGLE_CAMPUS";

    [Column("visit_type")]
    public string VisitType { get; set; } = "CAMPUS_TOUR";

    [Column("visit_type_other")]
    public string? VisitTypeOther { get; set; }
    [Column("purpose")]
    public string Purpose { get; set; } = null!;

    [Column("working_content")]
    public string? WorkingContent { get; set; }


    [Column("contact_person_full_name")]
    public string ContactPersonFullName { get; set; } = null!;

    [Column("contact_person_organization")]
    public string ContactPersonOrganization { get; set; } = null!;

    [Column("contact_person_phone")]
    public string ContactPersonPhone { get; set; } = null!;

    [Column("contact_person_email")]
    public string ContactPersonEmail { get; set; } = null!;

    [Column("working_language")]
    public string WorkingLanguage { get; set; } = "EN";


    // Free text the guest enters to describe/identify the transportation to FPTU
    // (replaces the old transportation_type enum + transportation_detail).
    [Column("transportation_note")]
    public string? TransportationNote { get; set; }

    [Column("media_consent_status")]
    public string MediaConsentStatus { get; set; } = "DECLINED";

    [Column("media_consent_note")]
    public string? MediaConsentNote { get; set; }

    [Column("note_to_fptu")]
    public string? NoteToFptu { get; set; }

    // Aggregate status only (PENDING_APPROVAL/PARTIALLY_APPROVED/APPROVED/REJECTED/CANCELLED),
    // derived from campus-instance decisions. The real approve/reject decision fields
    // (decided_by/decided_at/decision_actor_role/decision_note) live on VisitRequestCampus.
    [Column("status")]
    public string Status { get; set; } = "PENDING_APPROVAL";

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [Column("email_verified_at")]
    public DateTime? EmailVerifiedAt { get; set; }

    // --- Cancellation (UC-136, post-approval only). cancellation_reason carries
    // both the reason and any external-confirmation details; there is no separate note column. ---
    [Column("cancelled_by")]
    public ulong? CancelledBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    // --- Resubmission (SQL v10 resubmit_agenda_cancel24): the Visitor may edit & resubmit a
    // request after ALL campuses rejected it. Old per-campus decisions are snapshotted into
    // audit_log_changes before being cleared. ---
    [Column("resubmission_count")]
    public uint ResubmissionCount { get; set; }

    [Column("last_resubmitted_at")]
    public DateTime? LastResubmittedAt { get; set; }

    [Column("last_resubmitted_by")]
    public ulong? LastResubmittedBy { get; set; }

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

    public virtual Partner? Partner { get; set; }
    public virtual ICollection<VisitRequestCampus> CampusInstances { get; set; } = new List<VisitRequestCampus>();
    public virtual ICollection<VisitGuestMember> GuestMembers { get; set; } = new List<VisitGuestMember>();
}

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
    public ulong VisitorUserId { get; set; }

    [Column("partner_id")]
    public ulong? PartnerId { get; set; }

    [Column("registrant_full_name")]
    public string RegistrantFullName { get; set; } = null!;

    [Column("registrant_nationality")]
    public string? RegistrantNationality { get; set; }

    [Column("registrant_organization")]
    public string RegistrantOrganization { get; set; } = null!;

    [Column("registrant_job_title")]
    public string? RegistrantJobTitle { get; set; }

    [Column("registrant_phone")]
    public string? RegistrantPhone { get; set; }

    [Column("registrant_email")]
    public string RegistrantEmail { get; set; } = null!;

    [Column("delegation_name")]
    public string DelegationName { get; set; } = null!;

    [Column("visit_scope")]
    public string VisitScope { get; set; } = "SINGLE_CAMPUS";

    [Column("purpose")]
    public string Purpose { get; set; } = null!;

    [Column("working_content")]
    public string? WorkingContent { get; set; }

    [Column("expected_guest_count")]
    public int ExpectedGuestCount { get; set; } = 1;

    [Column("support_team_json")]
    public string? SupportTeamJson { get; set; }

    [Column("contact_person_json")]
    public string? ContactPersonJson { get; set; }

    [Column("working_language")]
    public string WorkingLanguage { get; set; } = "EN";

    [Column("interpreter_note")]
    public string? InterpreterNote { get; set; }

    [Column("transportation_note")]
    public string? TransportationNote { get; set; }

    [Column("note_to_fptu")]
    public string? NoteToFptu { get; set; }

    // Request decision status only (PENDING_APPROVAL/APPROVED/REJECTED/CANCELLED).
    // Visit progress is derived from VisitRequestCampus.Status.
    [Column("status")]
    public string Status { get; set; } = "PENDING_APPROVAL";

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [Column("email_verified_at")]
    public DateTime? EmailVerifiedAt { get; set; }

    [Column("decided_by")]
    public ulong? DecidedBy { get; set; }

    [Column("decided_at")]
    public DateTime? DecidedAt { get; set; }

    [Column("decision_actor_role")]
    public string? DecisionActorRole { get; set; }

    [Column("decision_note")]
    public string? DecisionNote { get; set; }

    // --- Cancellation (UC-136, post-approval only). cancellation_reason carries
    // both the reason and any external-confirmation details; there is no separate note column. ---
    [Column("cancelled_by")]
    public ulong? CancelledBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancellation_actor_type")]
    public string? CancellationActorType { get; set; }

    [Column("cancellation_source")]
    public string? CancellationSource { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

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
    public virtual ICollection<VisitStatusLog> StatusLogs { get; set; } = new List<VisitStatusLog>();
}

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

    // UUID of ONE submit intent (frontend crypto.randomUUID(), kept across
    // initiate/resend/recover/verify). UNIQUE in the DB — the last barrier against
    // double-click / network-retry creating a second row. NULL on legacy rows.
    [Column("submission_id")]
    public string? SubmissionId { get; set; }

    // SHA-256 "v1" fingerprint of the core visit identity (registrant email, effective
    // contact email, delegation name, scope, type(+other), sorted campus schedules).
    // NORMAL index only — never unique (would block legitimate later re-submissions).
    [Column("business_fingerprint")]
    public string? BusinessFingerprint { get; set; }

    // PRIMARY CONTACT owner (đầu mối chính quản lý yêu cầu). Always a VISITOR account.
    // NULL until contact B claims the request (PrimaryContactAccessStatus = PENDING_CONFIRMATION).
    // Once the contact is ACTIVE this is the cancel owner. It is NO LONGER the sole form editor:
    // per-campus form v2 makes the registrant a co-editor (see RegistrantUserId). Every mutation
    // still re-checks the relation from the DB at request time — never from a cached JWT claim.
    [Column("visitor_user_id")]
    public ulong? VisitorUserId { get; set; }

    // REGISTRANT (người đăng ký / submitter). NOT read-only under per-campus form v2: a co-editor
    // together with the primary contact for form edit / resubmit / safe-edit / amendment (enforced
    // per lifecycle by the write handlers in PR-4+). May also cancel under exception 3A while the
    // initial contact is still PENDING_CONFIRMATION (DB trigger enforces it). May be a VISITOR or an
    // internal STAFF/STAFF LEADER account.
    [Column("registrant_user_id")]
    public ulong? RegistrantUserId { get; set; }

    [Column("partner_id")]
    public ulong? PartnerId { get; set; }

    [Column("created_source")]
    public string CreatedSource { get; set; } = "VISITOR_SUBMITTED";

    // Pure V2: there is no form-version discriminator column. Every request is per-campus and its form
    // content lives in VisitInstanceFormDetail. HasMixedCampusDetails is backend-derived only.
    [Column("has_mixed_campus_details")]
    public bool HasMixedCampusDetails { get; set; }

    [Column("registrant_full_name")]
    public string RegistrantFullName { get; set; } = null!;

    [Column("registrant_nationality")]
    public string RegistrantNationality { get; set; } = null!;

    [Column("registrant_organization")]
    public string RegistrantOrganization { get; set; } = null!;

    [Column("registrant_job_title")]
    public string RegistrantJobTitle { get; set; } = null!;

    [Column("registrant_phone")]
    public string? RegistrantPhone { get; set; }

    [Column("registrant_email")]
    public string RegistrantEmail { get; set; } = null!;

    // ── PURE V2 ────────────────────────────────────────────────────────────────
    // Delegation name, visit type, purpose, working content, operational contact, language,
    // transportation, media consent and note-to-FPTU are per campus and live ONLY in
    // VisitInstanceFormDetail. The 10 global-form columns no longer exist in the schema; read form
    // content through IVisitFormReadService, never from the request row.
    [Column("visit_scope")]
    public string VisitScope { get; set; } = "SINGLE_CAMPUS";

    // PRIMARY CONTACT snapshot (đầu mối chính) at REQUEST level — the email used to link/claim the
    // VISITOR account (see VisitorUserId + PrimaryContactAccessStatus). This is NOT a campus
    // operational contact; each campus keeps its own in VisitInstanceFormDetail.OperationalContact*.
    [Column("contact_person_full_name")]
    public string ContactPersonFullName { get; set; } = null!;

    [Column("contact_person_organization")]
    public string ContactPersonOrganization { get; set; } = null!;

    [Column("contact_person_phone")]
    public string? ContactPersonPhone { get; set; }

    [Column("contact_person_email")]
    public string ContactPersonEmail { get; set; } = null!;

    // Primary-contact claim state (per-campus form v2). PENDING_CONFIRMATION = contact B has not
    // claimed the request yet; ACTIVE = contact owner confirmed. Backfilled ACTIVE where
    // VisitorUserId IS NOT NULL. See PrimaryContactAccessStatuses.
    [Column("primary_contact_access_status")]
    public string PrimaryContactAccessStatus { get; set; } = "PENDING_CONFIRMATION";

    [Column("primary_contact_verified_at")]
    public DateTime? PrimaryContactVerifiedAt { get; set; }

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

    // Per-campus form v2 navigations.
    public virtual ICollection<VisitRequestIdentityChange> IdentityChanges { get; set; } = new List<VisitRequestIdentityChange>();
    public virtual ICollection<VisitRequestRevisionHistory> RevisionHistory { get; set; } = new List<VisitRequestRevisionHistory>();
}

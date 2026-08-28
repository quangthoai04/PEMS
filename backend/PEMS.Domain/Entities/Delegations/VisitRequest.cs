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

    // REGISTRANT (người đăng ký / submitter) — the ONLY request-level owner. Sees every campus,
    // edits the request-level part, replaces contacts, and is the sole actor allowed to cancel the
    // whole request (DB trigger enforces it). May be a VISITOR, STAFF or STAFF LEADER; the creator's
    // role never bypasses the contact confirmation gate.
    //
    // Per-campus operating rights are NOT here — they live on
    // VisitRequestCampus.OperationalContactUserId. Every mutation re-checks the relation from the DB
    // at request time, never from a cached JWT claim.
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
    // transportation and media consent are per campus and live ONLY in VisitInstanceFormDetail.
    // The global-form columns no longer exist in the schema; read form content through
    // IVisitFormReadService, never from the request row.
    [Column("visit_scope")]
    public string VisitScope { get; set; } = "SINGLE_CAMPUS";

    // Global confirmation-gate revision. Bumped in the SAME transaction that opens the gate (the
    // last operational contact confirms) and again whenever the gate closes (a confirmed contact is
    // replaced before any campus decision). Used as the approval-notification dedupe key
    // APPROVAL_READY:{requestId}:{visitInstanceId}:{gateRevision} so re-opening the gate can send
    // again while a retry inside one opening cannot.
    [Column("contact_gate_revision")]
    public uint ContactGateRevision { get; set; }

    // Aggregate status only (PENDING_CONTACT_CONFIRMATION/PENDING_APPROVAL/PARTIALLY_APPROVED/
    // APPROVED/REJECTED/CANCELLED), derived from campus-instance state. The real approve/reject
    // decision fields live on VisitRequestCampus. While this is PENDING_CONTACT_CONFIRMATION no
    // Staff Leader may DECIDE any campus of the request; they may still read it.
    [Column("status")]
    public string Status { get; set; } = "PENDING_CONTACT_CONFIRMATION";

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

    // Contact confirmation invitations hang off the CAMPUS, not the request — see
    // VisitRequestCampus.IdentityChanges. Exposing a request-level collection here would let a
    // caller treat one campus's invitation as if it governed the whole request, which is exactly
    // the model this cutover removed.
    public virtual ICollection<VisitRequestRevisionHistory> RevisionHistory { get; set; } = new List<VisitRequestRevisionHistory>();
}

using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.GetSubmittedVisitRequestFormDetail;

/// <summary>
/// Read-only snapshot of exactly what the guest submitted in the visit-request form.
/// Reused by several screens (pre-approval review, approved/waiting-host detail, rejected
/// detail). It only ever reads visit_requests / visit_request_campuses / visit_guest_members
/// — never host-created data (agendas, participants, logistics, minutes, …).
///
/// Visibility/scope is enforced in the handler; the Can* flags only tell the UI which footer
/// actions to show (the actual approve/reject/assign-host still go through their own commands).
/// </summary>
public sealed class SubmittedVisitRequestFormDetailDto
{
    // ── System info ──
    public long VisitRequestId { get; set; }
    public string RequestCode { get; set; } = "";
    public string? CreatedSource { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public string RequestStatus { get; set; } = "";
    public string VisitScope { get; set; } = "";

    // ── Form schema version (per-campus form v2) ──
    // A request whose campuses differ throws FORM_VERSION_UPGRADE_REQUIRED before this DTO builds, so a
    // returned DTO always describes uniform per-campus content.
    public bool HasMixedCampusDetails { get; set; }

    // ── Visit info ──
    public string DelegationName { get; set; } = "";
    public string? VisitType { get; set; }
    public string? VisitTypeOther { get; set; }
    public string? Purpose { get; set; }
    public string? WorkingContent { get; set; }

    public SubmittedRegistrantDto Registrant { get; set; } = new();
    public SubmittedContactPersonDto ContactPerson { get; set; } = new();

    // ── Requirements & confirmations (all guest-entered) ──
    public string? WorkingLanguage { get; set; }
    public string? MediaConsentStatus { get; set; }
    public string? MediaConsentNote { get; set; }
    /// <summary>Free text the guest entered to identify the transportation to FPTU
    /// (replaces the old transportation type enum + detail).</summary>
    public string? TransportationNote { get; set; }
    public string? NoteToFptu { get; set; }

    public List<SubmittedCampusScheduleDto> Campuses { get; set; } = new();
    /// <summary>Per-campus decision counters (campus-independent approval).</summary>
    public CampusDecisionSummaryDto CampusDecisionSummary { get; set; } = new();
    public List<SubmittedGuestMemberDto> GuestMembers { get; set; } = new();
    public List<SubmittedGuestMemberDto> ExternalSupportMembers { get; set; } = new();

    // ── Decision info (campus-independent approval: decisions live on each campus instance).
    // These request-level fields mirror the SINGLE visible instance (single-campus requests or a
    // Staff Leader's own instance) for backward UI compatibility; multi-campus decision detail
    // rides on each SubmittedCampusScheduleDto. Reject reason = decision_note, NEVER cancellation_reason. ──
    public long? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public string? DecisionActorRole { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }

    // ── Cancellation info (UC-136, post-approval only) ──
    // Reflects either a request-level cancel (whole request CANCELLED) or a campus-instance-level
    // cancel (request still APPROVED, one campus cancelled). Cancel reason = cancellation_reason,
    // NEVER decision_note. Per-campus cancel detail also rides on each SubmittedCampusScheduleDto.
    public bool IsCancelled { get; set; }
    /// <summary>"REQUEST" or "CAMPUS_INSTANCE" (null when nothing is cancelled).</summary>
    public string? CancellationLevel { get; set; }
    public long? CancelledByUserId { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationActorType { get; set; }
    public string? CancellationSource { get; set; }
    public string? CancellationReason { get; set; }

    // ── Resubmission info (SQL v10 resubmit_agenda_cancel24) ──
    public int ResubmissionCount { get; set; }
    public DateTime? LastResubmittedAt { get; set; }
    public long? LastResubmittedBy { get; set; }
    public string? LastResubmittedByName { get; set; }

    // ── Footer action gating (recomputed server-side) ──
    // Approve = approve + assign host in ONE action (no separate assign-host step anymore).
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanCancel { get; set; }
}

/// <summary>Counters over the request's campus instances (campus-independent approval).</summary>
public sealed class CampusDecisionSummaryDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Cancelled { get; set; }
}

public sealed class SubmittedRegistrantDto
{
    public string? FullName { get; set; }
    public string? Organization { get; set; }
    public string? JobTitle { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Nationality { get; set; }
}

public sealed class SubmittedContactPersonDto
{
    public string? FullName { get; set; }
    public string? Organization { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed class SubmittedCampusScheduleDto
{
    public long VisitInstanceId { get; set; }
    public long CampusId { get; set; }
    public string CampusCode { get; set; } = "";
    public string CampusName { get; set; } = "";
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public string InstanceStatus { get; set; } = "";
    public long? CoordinatorUserId { get; set; }
    public long? CurrentHostUserId { get; set; }
    public string? CurrentHostName { get; set; }
    /// <summary>True for the campus instance that belongs to the calling Staff Leader's campus.</summary>
    public bool IsOwnCampus { get; set; }

    // ── Per-campus decision info (campus-independent approval) ──
    public long? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionActorRole { get; set; }
    public string? DecisionNote { get; set; }

    // ── Per-campus cancellation info (UC-136 instance-level cancel) ──
    public long? CancelledByUserId { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationActorType { get; set; }
    public string? CancellationSource { get; set; }
    public string? CancellationReason { get; set; }
}

public sealed class SubmittedGuestMemberDto
{
    public long GuestMemberId { get; set; }
    public string MemberType { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Organization { get; set; }
    public string? JobTitle { get; set; }
    public string? Nationality { get; set; }
    public int DisplayOrder { get; set; }
}

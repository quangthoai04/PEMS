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
    public string? TransportationType { get; set; }
    public string? TransportationDetail { get; set; }
    public string? NoteToFptu { get; set; }

    public List<SubmittedCampusScheduleDto> Campuses { get; set; } = new();
    public List<SubmittedGuestMemberDto> GuestMembers { get; set; } = new();
    public List<SubmittedGuestMemberDto> ExternalSupportMembers { get; set; } = new();

    // ── Decision info (only populated when the request has been decided) ──
    public long? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public string? DecisionActorRole { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }

    // ── Cancellation info (UC-136, post-approval only) ──
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    // ── Footer action gating (recomputed server-side) ──
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanAssignHost { get; set; }
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
    /// <summary>True for the campus instance that belongs to the calling Staff Leader's campus.</summary>
    public bool IsOwnCampus { get; set; }
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

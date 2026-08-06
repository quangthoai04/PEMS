using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.GetEditableVisitRequestDetail;

/// <summary>
/// Form-shaped snapshot of a visit request for the Visitor edit / resubmit screens.
/// Field names mirror the UC-17 submit payload so the frontend can hydrate the same
/// react-hook-form used for a new registration.
/// </summary>
public sealed class EditableVisitRequestDetailDto
{
    public long VisitRequestId { get; set; }
    public string RequestCode { get; set; } = "";
    public string RequestStatus { get; set; } = "";
    public string VisitScope { get; set; } = "";

    /// <summary>"EDIT" (still fully pending) or "RESUBMIT" (fully rejected).</summary>
    public string Mode { get; set; } = "";
    public bool IsEditablePending { get; set; }
    public bool IsResubmittable { get; set; }

    // ── Registrant ──
    public string RegistrantFullName { get; set; } = "";
    public string RegistrantNationality { get; set; } = "";
    public string RegistrantOrganization { get; set; } = "";
    public string RegistrantJobTitle { get; set; } = "";
    public string RegistrantPhone { get; set; } = "";
    public string RegistrantEmail { get; set; } = "";

    // ── Visit info ──
    public string DelegationName { get; set; } = "";
    public string VisitType { get; set; } = "";
    public string? VisitTypeOther { get; set; }
    public string Purpose { get; set; } = "";
    public string? WorkingContent { get; set; }

    // The contact is NOT here. It belongs to a campus, so it is carried by each
    // EditableCampusSlotDto — a request-level field could only prefill every campus from one of them.

    // ── Additional ──
    public string WorkingLanguage { get; set; } = "EN";
    public string? TransportationNote { get; set; }
    public string MediaConsentStatus { get; set; } = "DECLINED";
    public string? MediaConsentNote { get; set; }
    public long? PartnerId { get; set; }
    /// <summary>Display name of the linked partner (null when the request has no partner_id).</summary>
    public string? PartnerName { get; set; }
    /// <summary>True only when the linked partner is still ACTIVE + APPROVED (safe to keep as EXISTING_PARTNER).</summary>
    public bool PartnerIsActive { get; set; }
    /// <summary>Linked partner's profile status (APPROVED/…), so the FE can decide whether to keep the link.</summary>
    public string? PartnerProfileStatus { get; set; }

    public List<EditableCampusSlotDto> CampusVisits { get; set; } = new();
    public List<EditableGuestMemberDto> Visitors { get; set; } = new();
    public List<EditableGuestMemberDto> SupportMembers { get; set; } = new();

    // ── Resubmission info ──
    public int ResubmissionCount { get; set; }
    public DateTime? LastResubmittedAt { get; set; }

    /// <summary>
    /// RESUBMIT mode only: the old per-campus rejection decisions, shown as a banner so the
    /// Visitor knows why the request was rejected before editing. (The same snapshot is also
    /// written to audit_log_changes when the resubmit command clears the decision fields.)
    /// </summary>
    public List<PreviousCampusDecisionDto> PreviousDecisions { get; set; } = new();
}

public sealed class EditableCampusSlotDto
{
    public long VisitInstanceId { get; set; }
    public long CampusId { get; set; }
    /// <summary>Campus CODE (e.g. "HN") — the form works with codes, like the submit payload.</summary>
    public string CampusCode { get; set; } = "";
    public string CampusName { get; set; } = "";
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public string InstanceStatus { get; set; } = "";

    // ── This campus's operational contact (its own snapshot, never a sibling's) ──
    public string OperationalContactFullName { get; set; } = "";
    public string OperationalContactOrganization { get; set; } = "";
    public string OperationalContactPhone { get; set; } = "";
    public string OperationalContactEmail { get; set; } = "";
    /// <summary>
    /// True once an account holds this campus. The editor uses it to warn that changing the email
    /// re-opens the confirmation for this campus (and, until it is answered, for the whole request).
    /// </summary>
    public bool ContactConfirmed { get; set; }
}

public sealed class EditableGuestMemberDto
{
    public string FullName { get; set; } = "";
    public string? Organization { get; set; }
    public string? JobTitle { get; set; }
    public string? Nationality { get; set; }
}

public sealed class PreviousCampusDecisionDto
{
    public long VisitInstanceId { get; set; }
    public long CampusId { get; set; }
    public string CampusName { get; set; } = "";
    public string? DecisionNote { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
}

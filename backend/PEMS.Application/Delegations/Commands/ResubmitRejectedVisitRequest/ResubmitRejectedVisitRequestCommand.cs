using MediatR;
using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequest;

/// <summary>
/// Visitor edits &amp; RESUBMITS their own request after the WHOLE request was rejected
/// (request REJECTED + every campus instance REJECTED). The campus SET must stay identical
/// to the original (changing campuses ⇒ create a new request); only times/form fields may
/// change. Old per-campus decisions are snapshotted into audit_log_changes BEFORE the
/// decision fields are cleared, then everything goes back to PENDING_APPROVAL /
/// WAITING_REQUEST_APPROVAL and resubmission_count increments.
/// </summary>
public sealed record ResubmitRejectedVisitRequestCommand(
    // ── Registration info ──────────────────────────────────
    string RegistrantFullName,
    string RegistrantNationality,
    string RegistrantOrganization,
    string RegistrantPosition,
    string RegistrantPhone,
    string RegistrantEmail,

    // ── Visit info ─────────────────────────────────────────
    string DelegationName,
    string VisitScope,
    string VisitType,
    string? VisitTypeOther,
    IList<VisitSlotDto> CampusVisits,
    string Purpose,
    string? WorkingContent,

    // ── Attendees ──────────────────────────────────────────
    IList<VisitorDto> Visitors,
    IList<SupportTeamMemberDto> SupportMembers,

    // ── Contact point ──────────────────────────────────────
    ContactPointDto ContactPerson,
    bool IsContactSelf,

    // ── Additional ─────────────────────────────────────────
    string WorkingLanguage,
    string? TransportationNote,
    string MediaConsentStatus,
    string? MediaConsentNote,
    ulong? PartnerId,
    string? Notes
) : IRequest<ResubmitRejectedVisitRequestResponse>, IVisitRequestFormCommand
{
    /// <summary>Route id — stamped by the controller, never bound from the body.</summary>
    public ulong VisitRequestId { get; init; }
}

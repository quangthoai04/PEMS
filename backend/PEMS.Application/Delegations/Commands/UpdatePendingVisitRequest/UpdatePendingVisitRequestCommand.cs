using MediatR;
using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands.UpdatePendingVisitRequest;

/// <summary>
/// Visitor edits their OWN visit request while it is still fully pending:
/// request PENDING_APPROVAL, every campus instance WAITING_REQUEST_APPROVAL and the
/// earliest planned start ≥ 24h away. The payload mirrors the UC-17 submit form
/// (<see cref="IVisitRequestFormCommand"/>) — campus list MAY change here because no
/// campus has decided anything yet. Never touches resubmission_count.
/// </summary>
public sealed record UpdatePendingVisitRequestCommand(
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
) : IRequest<UpdatePendingVisitRequestResponse>, IVisitRequestFormCommand
{
    /// <summary>Route id — stamped by the controller, never bound from the body.</summary>
    public ulong VisitRequestId { get; init; }
}

using MediatR;
using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequest;

/// <summary>
/// Step 1 of UC-17: stores the form data, generates a 6-digit OTP and sends it
/// to the registrant's email. Returns a <c>SessionToken</c> (PendingId) the
/// frontend must supply at the verify step.
/// </summary>
public sealed record InitiateVisitRequestCommand(
    // ── Registration info ──────────────────────────────────
    string RegisterFullName,
    string RegisterNationality,
    string RegisterOrganization,
    string RegisterJobTitle,
    string RegisterPhone,
    string RegisterEmail,

    // ── Visit info ─────────────────────────────────────────
    string DelegationName,
    string VisitScope,
    IList<VisitSlotDto> VisitSlots,
    string Purpose,
    string? WorkingContent,

    // ── Attendees ──────────────────────────────────────────
    IList<VisitorDto> Visitors,
    IList<SupportTeamMemberDto> SupportTeam,

    // ── Contact point ──────────────────────────────────────
    ContactPointDto ContactPoint,
    bool IsContactSelf,

    // ── Additional ─────────────────────────────────────────
    string Language,
    string? Vehicle,
    string? Notes
) : IRequest<InitiateVisitRequestResponse>;

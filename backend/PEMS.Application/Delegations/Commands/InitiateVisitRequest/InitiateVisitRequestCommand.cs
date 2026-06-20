using MediatR;
using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequest;

/// <summary>
/// Step 1 of UC-17: validates the form, generates a 6-digit OTP and emails it to the
/// registrant. Nothing is persisted except the OTP (otp_tokens) — the form draft stays
/// on the frontend (sessionStorage) and is resubmitted at the verify step.
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
) : IRequest<InitiateVisitRequestResponse>, IVisitRequestFormCommand;

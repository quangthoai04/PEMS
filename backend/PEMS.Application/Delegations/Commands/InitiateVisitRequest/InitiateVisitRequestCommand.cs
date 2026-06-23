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
    string TransportationType,
    string? TransportationDetail,
    string MediaConsentStatus,
    string? MediaConsentNote,
    ulong? PartnerId,
    string? Notes
) : IRequest<InitiateVisitRequestResponse>, IVisitRequestFormCommand;

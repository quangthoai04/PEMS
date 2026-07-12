using MediatR;
using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands.CreateAuthenticatedVisitRequest;

/// <summary>
/// Per-campus processing choice for an AUTHENTICATED create. <see cref="CampusId"/> is
/// the campus CODE (same convention as <see cref="VisitSlotDto"/>). Modes are validated
/// against the caller's role/campus server-side (see CampusSubmissionModes):
///   SEND_FOR_REVIEW — default, campus Staff Leader decides later.
///   SELF_HOST       — creator is the official host (own campus only).
///   ASSIGN_HOST     — Staff Leader assigns another same-campus IC Staff (own campus only).
/// </summary>
public sealed record CampusProcessingDto(
    string CampusId,
    string Mode,
    ulong? HostUserId);

/// <summary>
/// Authenticated visit-request create (Visitor / IC Staff / Staff Leader) — no OTP: the
/// session identity IS the registrant. The registrant full name/email in the payload are
/// display-only and always overridden from the authenticated user's DB record; only the
/// organization/job title/phone/nationality snapshot fields are taken from the form.
/// The contact person always is/becomes a VISITOR account (the request's action owner).
/// </summary>
public sealed record CreateAuthenticatedVisitRequestCommand(
    // ── Registrant snapshot (identity fields are server-overridden) ──
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

    // ── Contact point (action owner; internal actors must name someone else) ──
    ContactPointDto ContactPerson,
    bool IsContactSelf,

    // ── Additional ─────────────────────────────────────────
    string WorkingLanguage,
    string? TransportationNote,
    string MediaConsentStatus,
    string? MediaConsentNote,
    ulong? PartnerId,
    string? Notes,

    // ── Per-campus processing modes (empty/missing entries default to SEND_FOR_REVIEW) ──
    IList<CampusProcessingDto>? CampusProcessing,

    // Direct-assignment host conflict is a non-blocking warning: the client must resubmit
    // with this flag after showing the user the confirmation dialog.
    bool ConfirmedHostConflict,

    // UUID of THIS submit intent — same idempotency contract as the public flow.
    string SubmissionId
) : IRequest<CreateAuthenticatedVisitRequestResponse>, IVisitRequestFormCommand;

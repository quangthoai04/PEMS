using MediatR;
using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;

/// <summary>
/// Step 2 of UC-17: verifies the OTP, then creates the VisitRequest, provisions the
/// Visitor account, routes to the correct approval queue, and sends confirmation.
/// SQL v8.3 has no pending_visit_requests table, so the full form is resubmitted here
/// (the frontend keeps it in sessionStorage between the two steps).
/// </summary>
public sealed record VerifyAndCreateVisitRequestCommand(
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
    string? Notes,

    // ── Verification ───────────────────────────────────────
    string OtpCode,

    // UUID of THIS submit intent (same value used at /initiate). Idempotency key:
    // retries of the same intent can never create a second request.
    string SubmissionId,

    // Opaque challenge token returned by /initiate (or resend/recover). Identifies the
    // OTP challenge row — the backend no longer picks "latest OTP for this email".
    string SessionToken
) : IRequest<VerifyAndCreateVisitRequestResponse>, IVisitRequestFormCommand;

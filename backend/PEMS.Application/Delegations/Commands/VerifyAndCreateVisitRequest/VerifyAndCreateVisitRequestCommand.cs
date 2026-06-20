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
    string? Notes,

    // ── Verification ───────────────────────────────────────
    string OtpCode
) : IRequest<VerifyAndCreateVisitRequestResponse>, IVisitRequestFormCommand;

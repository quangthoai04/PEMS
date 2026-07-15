using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.Commands.CreateAuthenticatedVisitRequest;
using PEMS.Application.Delegations.Commands.InitiateVisitRequest;
using PEMS.Application.Delegations.Commands.RecoverVisitRequestOtp;
using PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequest;
using PEMS.Application.Delegations.Commands.UpdatePendingVisitRequest;
using PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;
using PEMS.Application.Delegations.Queries.GetCreateHostCandidates;
using PEMS.Application.Delegations.Queries.GetEditableVisitRequestDetail;
using PEMS.Application.Delegations.Queries.GetVisitRequestFormV2;

namespace PEMS.Api.Controllers;

/// <summary>
/// UC-17 — Public endpoint for submitting a visit request (no authentication required),
/// plus the authenticated Visitor edit / resubmit flow (ownership + role are enforced in
/// the handlers via ICurrentUserService — an anonymous caller gets 403).
/// </summary>
[ApiController]
[Route("api/visit-requests")]
public sealed class VisitRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public VisitRequestsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// STEP 1 — Validates form data, stores a pending session and sends a 6-digit OTP
    /// to the registrant's email. Returns a <c>sessionToken</c> the client must pass
    /// to the verify endpoint.
    /// </summary>
    [HttpPost("initiate")]
    [ProducesResponseType(typeof(InitiateVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Initiate(
        [FromBody] InitiateVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// STEP 2 — Verifies the OTP challenge (identified by <c>sessionToken</c> and bound to
    /// <c>submissionId</c>), creates the VisitRequest, provisions the Visitor account and
    /// routes the request to the correct approval queue. Retries of the SAME submission
    /// intent are replayed idempotently (200 with the original request). A different submit
    /// intent with the same core content inside the duplicate window returns
    /// 409 DUPLICATE_VISIT_REQUEST.
    /// </summary>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(VerifyAndCreateVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyAndCreateVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// RESEND — Supersedes the old challenge and returns a NEW <c>sessionToken</c> with a
    /// fresh code. Subject to per-email hourly issue quotas + min resend interval. A
    /// challenge that already requires human verification cannot be resent (428).
    /// </summary>
    [HttpPost("resend-otp")]
    [ProducesResponseType(typeof(InitiateVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendOtp(
        [FromBody] ResendVisitRequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// RECOVER — After the challenge was burned by too many wrong codes: validates the
    /// Turnstile token server-side, invalidates the old challenge and issues a brand-new
    /// one (attempts reset) for the same submission intent. CAPTCHA success never re-opens
    /// the old code.
    /// </summary>
    [HttpPost("otp/recover")]
    [ProducesResponseType(typeof(InitiateVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RecoverOtp(
        [FromBody] RecoverVisitRequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// AUTHENTICATED create (Visitor / IC Staff / Staff Leader) — no OTP: the JWT session
    /// is the registrant identity. Same shared form validation + fingerprint idempotency
    /// as the public flow. Per-campus processing modes (SELF_HOST / ASSIGN_HOST) are only
    /// honoured on the caller's own campus and only for Staff/Staff Leader; the handler
    /// revalidates role, campus scope and host candidate from the DB.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CreateAuthenticatedVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAuthenticated(
        [FromBody] CreateAuthenticatedVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Host candidates for the authenticated create form's ASSIGN_HOST mode (Staff Leader
    /// only, own campus implied — no campus parameter, other campuses can't be probed).
    /// Optional planned window drives non-blocking schedule-conflict warnings.
    /// </summary>
    [HttpGet("host-candidates")]
    [Authorize]
    public async Task<IActionResult> GetCreateHostCandidates(
        [FromQuery] DateTime? startAt,
        [FromQuery] DateTime? endAt,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCreateHostCandidatesQuery(startAt, endAt), cancellationToken);
        return Ok(result);
    }

    // ── Visitor edit / resubmit (SQL v10 resubmit_agenda_cancel24) ────────────────
    // All three endpoints are OWNER-only (Visitor): the handlers verify authentication,
    // role, ownership and the editable/resubmittable state — no OTP round-trip here.

    /// <summary>
    /// Loads the full form data of the caller's own request so the edit (pending) or
    /// resubmit (rejected) form can be prefilled.
    /// </summary>
    [HttpGet("{visitRequestId}/edit-detail")]
    [Authorize]
    public async Task<IActionResult> GetEditableDetail(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEditableVisitRequestDetailQuery(visitRequestId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PR-3 reference read path — returns the fully per-campus resolved form via the central
    /// dual-read <c>IVisitFormReadService</c> (v1 or v2, correctly scoped to the caller). Gated by
    /// the <c>PerCampusFormV2</c> feature flag: 404 when the flag is OFF. v1 endpoints are unchanged.
    /// </summary>
    [HttpGet("/api/v2/visit-requests/{visitRequestId}")]
    [Authorize]
    public async Task<IActionResult> GetFormV2(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetVisitRequestFormV2Query(visitRequestId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Visitor edits a still-fully-pending request (every campus WAITING_REQUEST_APPROVAL,
    /// ≥ 24h before the earliest start). Campus list may change; status stays PENDING_APPROVAL.
    /// </summary>
    [HttpPut("{visitRequestId}/pending-edit")]
    [Authorize]
    public async Task<IActionResult> UpdatePending(
        ulong visitRequestId,
        [FromBody] UpdatePendingVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { VisitRequestId = visitRequestId }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Visitor edits &amp; resubmits a fully-rejected request. The campus set must stay the
    /// same; old decisions are snapshotted to audit before being cleared.
    /// </summary>
    [HttpPost("{visitRequestId}/resubmit")]
    [Authorize]
    public async Task<IActionResult> Resubmit(
        ulong visitRequestId,
        [FromBody] ResubmitRejectedVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { VisitRequestId = visitRequestId }, cancellationToken);
        return Ok(result);
    }
}

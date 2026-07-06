using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.Commands.InitiateVisitRequest;
using PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequest;
using PEMS.Application.Delegations.Commands.UpdatePendingVisitRequest;
using PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;
using PEMS.Application.Delegations.Queries.GetEditableVisitRequestDetail;

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
    /// STEP 2 — Verifies the OTP, creates the VisitRequest, provisions the Visitor
    /// account and routes the request to the correct approval queue.
    /// </summary>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(VerifyAndCreateVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyAndCreateVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// RESEND — Generates and sends a new OTP for an active pending session.
    /// Subject to the same hourly resend limit as the initial request (max 5/hr).
    /// </summary>
    [HttpPost("resend-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendOtp(
        [FromBody] ResendVisitRequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
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
    public async Task<IActionResult> GetEditableDetail(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEditableVisitRequestDetailQuery(visitRequestId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Visitor edits a still-fully-pending request (every campus WAITING_REQUEST_APPROVAL,
    /// ≥ 24h before the earliest start). Campus list may change; status stays PENDING_APPROVAL.
    /// </summary>
    [HttpPut("{visitRequestId}/pending-edit")]
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
    public async Task<IActionResult> Resubmit(
        ulong visitRequestId,
        [FromBody] ResubmitRejectedVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { VisitRequestId = visitRequestId }, cancellationToken);
        return Ok(result);
    }
}

using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.Commands.InitiateVisitRequest;
using PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;
using PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;

namespace PEMS.Api.Controllers;

/// <summary>
/// UC-17 — Public endpoint for submitting a visit request (no authentication required).
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
}

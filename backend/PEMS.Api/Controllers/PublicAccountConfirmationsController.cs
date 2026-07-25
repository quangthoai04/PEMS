using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Accounts.Commands.ConfirmAccountEmail;

namespace PEMS.Api.Controllers;

/// <summary>
/// Public (no-login) email-confirmation endpoint for pending internal accounts (P0 #1). Confirming
/// changes state, so it is POST only — there is deliberately no GET here (a GET must never activate an
/// account). The frontend confirm page reads the token from the URL and POSTs it as JSON.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/account-confirmations")]
public sealed class PublicAccountConfirmationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicAccountConfirmationsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(
        [FromBody] ConfirmAccountEmailCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

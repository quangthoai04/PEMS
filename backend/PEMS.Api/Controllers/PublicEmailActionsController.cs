using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Email;
using PEMS.Application.EmailActions;

namespace PEMS.Api.Controllers;

/// <summary>
/// Public (no-login) Accept/Decline landing for participant-invitation emails. GET shows a confirm
/// page; POST consumes the one-time token. Server-rendered HTML so it works straight from any email
/// client. The "Gán nhân sự" action is intentionally NOT here — assignment requires login.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/email-actions")]
public sealed class PublicEmailActionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicEmailActionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{token}")]
    public async Task<IActionResult> Show(string token, CancellationToken cancellationToken)
    {
        var info = await _mediator.Send(new GetEmailActionInfoQuery(token), cancellationToken);
        return Content(EmailActionHtmlPages.RenderLanding(info), "text/html; charset=utf-8");
    }

    [HttpPost("{token}")]
    public async Task<IActionResult> Execute(string token, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await _mediator.Send(new ExecuteEmailActionCommand(token, ip, userAgent), cancellationToken);
        return Content(EmailActionHtmlPages.RenderResult(result), "text/html; charset=utf-8");
    }
}

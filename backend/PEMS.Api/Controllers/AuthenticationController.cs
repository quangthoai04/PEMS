using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PEMS.Application.Authentication.Commands.ForgotPassword;
using PEMS.Application.Authentication.Commands.LoginviaCredentials;
using PEMS.Application.Authentication.Commands.LoginviaSSO;
using PEMS.Application.Authentication.Commands.Logout;
using PEMS.Application.Authentication.Commands.RefreshToken;
using PEMS.Application.Authentication.Commands.ResetPassword;
using PEMS.Application.Authentication.Queries.GetCurrentUser;
using PEMS.Application.Authentication.Queries.GetCurrentUserPermissions;

namespace PEMS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthenticationController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthenticationController(IMediator mediator) => _mediator = mediator;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginviaCredentialsCommand command, CancellationToken cancellationToken)
    {
        ApplyRequestContext(c => { command.IpAddress = c.ip; command.UserAgent = c.ua; });
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> Google([FromBody] LoginviaSSOCommand command, CancellationToken cancellationToken)
    {
        ApplyRequestContext(c => { command.IpAddress = c.ip; command.UserAgent = c.ua; });
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        ApplyRequestContext(c => { command.IpAddress = c.ip; command.UserAgent = c.ua; });
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand? command, CancellationToken cancellationToken)
    {
        command ??= new LogoutCommand();
        ApplyRequestContext(c => { command.IpAddress = c.ip; command.UserAgent = c.ua; });
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetCurrentUserQuery(), cancellationToken));

    [HttpGet("permissions")]
    [Authorize]
    public async Task<IActionResult> Permissions(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetCurrentUserPermissionsQuery(), cancellationToken));

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        ApplyRequestContext(c => { command.IpAddress = c.ip; command.UserAgent = c.ua; });
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ApplyRequestContext(c => { command.IpAddress = c.ip; command.UserAgent = c.ua; });
        return Ok(await _mediator.Send(command, cancellationToken));
    }


    private void ApplyRequestContext(Action<(string? ip, string? ua)> apply)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        apply((ip, string.IsNullOrWhiteSpace(ua) ? null : ua));
    }
}

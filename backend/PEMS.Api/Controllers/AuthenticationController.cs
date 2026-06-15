using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AuthenticationController(IMediator mediator) => _mediator = mediator;

        [HttpPost("loginviasso")]
        public async Task<IActionResult> LoginviaSSO([FromBody] PEMS.Application.Authentication.Commands.LoginviaSSO.LoginviaSSOCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("loginviacredentials")]
        public async Task<IActionResult> LoginviaCredentials([FromBody] PEMS.Application.Authentication.Commands.LoginviaCredentials.LoginviaCredentialsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] PEMS.Application.Authentication.Commands.Logout.LogoutCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("forgotpassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] PEMS.Application.Authentication.Commands.ForgotPassword.ForgotPasswordCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}

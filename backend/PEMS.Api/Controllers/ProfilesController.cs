using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using PEMS.Application.Profiles.Commands.ChangePassword;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfilesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ProfilesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewprofile")]
        public async Task<IActionResult> ViewProfile([FromQuery] PEMS.Application.Profiles.Queries.ViewProfile.ViewProfileQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateprofile")]
        public async Task<IActionResult> UpdateProfile([FromBody] PEMS.Application.Profiles.Commands.UpdateProfile.UpdateProfileCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken cancellationToken)
        {
            command.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers.UserAgent.ToString();
            command.UserAgent = string.IsNullOrWhiteSpace(ua) ? null : ua;

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}

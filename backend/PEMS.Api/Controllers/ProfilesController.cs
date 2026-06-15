using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

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

        [HttpPost("changepassword")]
        public async Task<IActionResult> ChangePassword([FromBody] PEMS.Application.Profiles.Commands.ChangePassword.ChangePasswordCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}

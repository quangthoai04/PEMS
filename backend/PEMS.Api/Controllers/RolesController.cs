using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public RolesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewrolelist")]
        public async Task<IActionResult> ViewRoleList([FromQuery] PEMS.Application.Roles.Queries.ViewRoleList.ViewRoleListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createnewrole")]
        public async Task<IActionResult> CreateNewRole([FromBody] PEMS.Application.Roles.Commands.CreateNewRole.CreateNewRoleCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("configurerolepermissions")]
        public async Task<IActionResult> ConfigureRolePermissions([FromBody] PEMS.Application.Roles.Commands.ConfigureRolePermissions.ConfigureRolePermissionsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateroledetails")]
        public async Task<IActionResult> UpdateRoleDetails([FromBody] PEMS.Application.Roles.Commands.UpdateRoleDetails.UpdateRoleDetailsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("disableanddeleterole")]
        public async Task<IActionResult> DisableAndDeleteRole([FromBody] PEMS.Application.Roles.Commands.DisableAndDeleteRole.DisableAndDeleteRoleCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}

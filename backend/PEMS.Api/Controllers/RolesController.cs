using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public RolesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewrolelist")]
        [RequirePermission(PermissionCodes.ViewRoleList, PermissionLevels.Read)]
        public async Task<IActionResult> ViewRoleList([FromQuery] PEMS.Application.Roles.Queries.ViewRoleList.ViewRoleListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createnewrole")]
        [RequirePermission(PermissionCodes.CreateRole, PermissionLevels.Execute)]
        public async Task<IActionResult> CreateNewRole([FromBody] PEMS.Application.Roles.Commands.CreateNewRole.CreateNewRoleCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("configurerolepermissions")]
        [RequirePermission(PermissionCodes.ConfigureRolePermissions, PermissionLevels.Execute)]
        public async Task<IActionResult> ConfigureRolePermissions([FromBody] PEMS.Application.Roles.Commands.ConfigureRolePermissions.ConfigureRolePermissionsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateroledetails")]
        [RequirePermission(PermissionCodes.UpdateRoleDetails, PermissionLevels.Execute)]
        public async Task<IActionResult> UpdateRoleDetails([FromBody] PEMS.Application.Roles.Commands.UpdateRoleDetails.UpdateRoleDetailsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("disableanddeleterole")]
        [RequirePermission(PermissionCodes.DisableDeleteRole, PermissionLevels.Execute)]
        public async Task<IActionResult> DisableAndDeleteRole([FromBody] PEMS.Application.Roles.Commands.DisableAndDeleteRole.DisableAndDeleteRoleCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}

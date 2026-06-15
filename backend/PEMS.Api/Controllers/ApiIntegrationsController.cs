using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApiIntegrationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ApiIntegrationsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewapiconfiguration")]
        public async Task<IActionResult> ViewAPIConfiguration([FromQuery] PEMS.Application.ApiIntegrations.Queries.ViewAPIConfiguration.ViewAPIConfigurationQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createapiconfiguration")]
        public async Task<IActionResult> CreateAPIConfiguration([FromBody] PEMS.Application.ApiIntegrations.Commands.CreateAPIConfiguration.CreateAPIConfigurationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateapiconfiguration")]
        public async Task<IActionResult> UpdateAPIConfiguration([FromBody] PEMS.Application.ApiIntegrations.Commands.UpdateAPIConfiguration.UpdateAPIConfigurationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("deleteapiconfiguration")]
        public async Task<IActionResult> DeleteAPIConfiguration([FromBody] PEMS.Application.ApiIntegrations.Commands.DeleteAPIConfiguration.DeleteAPIConfigurationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("testapiconnection")]
        public async Task<IActionResult> TestAPIConnection([FromBody] PEMS.Application.ApiIntegrations.Commands.TestAPIConnection.TestAPIConnectionCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("manageapistatus")]
        public async Task<IActionResult> ManageAPIStatus([FromBody] PEMS.Application.ApiIntegrations.Commands.ManageAPIStatus.ManageAPIStatusCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("configurerequestlimit")]
        public async Task<IActionResult> ConfigureRequestLimit([FromBody] PEMS.Application.ApiIntegrations.Commands.ConfigureRequestLimit.ConfigureRequestLimitCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewapilogs")]
        public async Task<IActionResult> ViewAPILogs([FromQuery] PEMS.Application.ApiIntegrations.Queries.ViewAPILogs.ViewAPILogsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchapilogs")]
        public async Task<IActionResult> SearchAPILogs([FromQuery] PEMS.Application.ApiIntegrations.Queries.SearchAPILogs.SearchAPILogsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}

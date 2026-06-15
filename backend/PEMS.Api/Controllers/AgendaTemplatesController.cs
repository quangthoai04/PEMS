using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgendaTemplatesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AgendaTemplatesController(IMediator mediator) => _mediator = mediator;

        [HttpPost("createagendatemplate")]
        public async Task<IActionResult> CreateAgendaTemplate([FromBody] PEMS.Application.AgendaTemplates.Commands.CreateAgendaTemplate.CreateAgendaTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateagendatemplate")]
        public async Task<IActionResult> UpdateAgendaTemplate([FromBody] PEMS.Application.AgendaTemplates.Commands.UpdateAgendaTemplate.UpdateAgendaTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("deleteagendatemplate")]
        public async Task<IActionResult> DeleteAgendaTemplate([FromBody] PEMS.Application.AgendaTemplates.Commands.DeleteAgendaTemplate.DeleteAgendaTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewagendatemplatelist")]
        public async Task<IActionResult> ViewAgendaTemplateList([FromQuery] PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateList.ViewAgendaTemplateListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewagendatemplatedetail")]
        public async Task<IActionResult> ViewAgendaTemplateDetail([FromQuery] PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDetail.ViewAgendaTemplateDetailQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}

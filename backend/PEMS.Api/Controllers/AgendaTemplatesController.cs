using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.AgendaTemplates.Commands.ApplyAgendaTemplate;
using PEMS.Application.AgendaTemplates.Commands.CreateAgendaTemplate;
using PEMS.Application.AgendaTemplates.Commands.DeleteAgendaTemplate;
using PEMS.Application.AgendaTemplates.Commands.SetAgendaTemplateDefault;
using PEMS.Application.AgendaTemplates.Commands.UpdateAgendaTemplate;
using PEMS.Application.AgendaTemplates.Queries.GetAgendaSetupForInstance;
using PEMS.Application.AgendaTemplates.Queries.GetDefaultAgendaTemplate;
using PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDefaults;
using PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDetail;
using PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateList;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Agenda template module (4 tables: agenda_templates, agenda_template_items,
    /// agenda_template_defaults, visit_agendas). Scope/role enforcement is handler-side
    /// (HO → GLOBAL, Staff Leader → own campus). The host applies a template into the concrete
    /// visit_agendas of a campus instance; visit_request_campuses is never written by this module.
    /// </summary>
    [ApiController]
    [Route("api/agenda-templates")]
    public class AgendaTemplatesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AgendaTemplatesController(IMediator mediator) => _mediator = mediator;

        // ── Template CRUD ────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAgendaTemplateCommand command, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(command, cancellationToken));

        [HttpPut("{agendaTemplateId:long}")]
        public async Task<IActionResult> Update(ulong agendaTemplateId, [FromBody] UpdateAgendaTemplateCommand command, CancellationToken cancellationToken)
        {
            command.AgendaTemplateId = agendaTemplateId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpDelete("{agendaTemplateId:long}")]
        public async Task<IActionResult> Delete(ulong agendaTemplateId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new DeleteAgendaTemplateCommand { AgendaTemplateId = agendaTemplateId }, cancellationToken));

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] ViewAgendaTemplateListQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        // ── Default-template management ──────────────────────────────────────
        [HttpGet("defaults")]
        public async Task<IActionResult> ViewDefaults([FromQuery] ViewAgendaTemplateDefaultsQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        [HttpPut("defaults")]
        public async Task<IActionResult> SetDefault([FromBody] SetAgendaTemplateDefaultCommand command, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(command, cancellationToken));

        [HttpGet("default")]
        public async Task<IActionResult> GetDefault([FromQuery] GetDefaultAgendaTemplateQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        // ── Visit-instance agenda setup / apply ──────────────────────────────
        [HttpGet("for-instance/{visitInstanceId:long}")]
        public async Task<IActionResult> GetSetupForInstance(ulong visitInstanceId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetAgendaSetupForInstanceQuery(visitInstanceId), cancellationToken));

        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] ApplyAgendaTemplateCommand command, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(command, cancellationToken));

        // ── Template detail (kept last; numeric route does not shadow /defaults | /default) ──
        [HttpGet("{agendaTemplateId:long}")]
        public async Task<IActionResult> Detail(ulong agendaTemplateId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ViewAgendaTemplateDetailQuery { AgendaTemplateId = agendaTemplateId }, cancellationToken));
    }
}

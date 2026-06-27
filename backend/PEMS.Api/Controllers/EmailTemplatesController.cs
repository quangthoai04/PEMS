using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Queries.PreviewEmailTemplate;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/email-templates")]
    [RoleAuthorize(
        EffectiveRole.StaffLeader,
        EffectiveRole.Staff,
        EffectiveRole.DepartmentLead,
        EffectiveRole.Department,
        EffectiveRole.Ho
    )]
    public class EmailTemplatesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public EmailTemplatesController(IMediator mediator) => _mediator = mediator;

        // Render a template's subject/body for the "Xem trước email" modal. Read-only — the real
        // send still happens only when the user presses "Mời" / "Gửi yêu cầu".
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] PreviewEmailTemplateQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] PEMS.Application.Emails.Queries.ViewEmailTemplateList.ViewEmailTemplateListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetail(ulong id, CancellationToken cancellationToken)
        {
            var query = new PEMS.Application.Emails.Queries.ViewEmailTemplateDetail.ViewEmailTemplateDetailQuery { EmailTemplateId = id };
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PEMS.Application.Emails.Commands.CreateEmailTemplate.CreateEmailTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(ulong id, [FromBody] PEMS.Application.Emails.Commands.UpdateEmailTemplate.UpdateEmailTemplateCommand command, CancellationToken cancellationToken)
        {
            command.EmailTemplateId = id;
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ToggleStatus(ulong id, [FromBody] PEMS.Application.Emails.Commands.ToggleEmailTemplateStatus.ToggleEmailTemplateStatusCommand command, CancellationToken cancellationToken)
        {
            command.EmailTemplateId = id;
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
    }
}

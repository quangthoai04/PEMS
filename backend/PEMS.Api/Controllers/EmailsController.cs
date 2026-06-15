using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public EmailsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewemailtemplatelist")]
        public async Task<IActionResult> ViewEmailTemplateList([FromQuery] PEMS.Application.Emails.Queries.ViewEmailTemplateList.ViewEmailTemplateListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewemailtemplatedetail")]
        public async Task<IActionResult> ViewEmailTemplateDetail([FromQuery] PEMS.Application.Emails.Queries.ViewEmailTemplateDetail.ViewEmailTemplateDetailQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateemailtemplate")]
        public async Task<IActionResult> UpdateEmailTemplate([FromBody] PEMS.Application.Emails.Commands.UpdateEmailTemplate.UpdateEmailTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createemailtemplate")]
        public async Task<IActionResult> CreateEmailTemplate([FromBody] PEMS.Application.Emails.Commands.CreateEmailTemplate.CreateEmailTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("editemailcontent")]
        public async Task<IActionResult> EditEmailContent([FromBody] PEMS.Application.Emails.Commands.EditEmailContent.EditEmailContentCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("sendemail")]
        public async Task<IActionResult> SendEmail([FromBody] PEMS.Application.Emails.Commands.SendEmail.SendEmailCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewemail")]
        public async Task<IActionResult> ViewEmail([FromQuery] PEMS.Application.Emails.Queries.ViewEmail.ViewEmailQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("replytoemail")]
        public async Task<IActionResult> ReplytoEmail([FromBody] PEMS.Application.Emails.Commands.ReplytoEmail.ReplytoEmailCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}

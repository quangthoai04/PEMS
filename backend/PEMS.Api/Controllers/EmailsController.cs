using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public EmailsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewemailtemplatelist")]
        [RequirePermission(PermissionCodes.ViewEmailTemplateList, PermissionLevels.Read)]
        public async Task<IActionResult> ViewEmailTemplateList([FromQuery] PEMS.Application.Emails.Queries.ViewEmailTemplateList.ViewEmailTemplateListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewemailtemplatedetail")]
        [RequirePermission(PermissionCodes.ViewEmailTemplateDetail, PermissionLevels.Read)]
        public async Task<IActionResult> ViewEmailTemplateDetail([FromQuery] PEMS.Application.Emails.Queries.ViewEmailTemplateDetail.ViewEmailTemplateDetailQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateemailtemplate")]
        [RequirePermission(PermissionCodes.UpdateEmailTemplate, PermissionLevels.Execute)]
        public async Task<IActionResult> UpdateEmailTemplate([FromBody] PEMS.Application.Emails.Commands.UpdateEmailTemplate.UpdateEmailTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createemailtemplate")]
        [RequirePermission(PermissionCodes.CreateEmailTemplate, PermissionLevels.Full)]
        public async Task<IActionResult> CreateEmailTemplate([FromBody] PEMS.Application.Emails.Commands.CreateEmailTemplate.CreateEmailTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("editemailcontent")]
        [RequirePermission(PermissionCodes.EditEmailContent, PermissionLevels.Own)]
        public async Task<IActionResult> EditEmailContent([FromBody] PEMS.Application.Emails.Commands.EditEmailContent.EditEmailContentCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("sendemail")]
        [RequirePermission(PermissionCodes.SendEmail, PermissionLevels.Own)]
        public async Task<IActionResult> SendEmail([FromBody] PEMS.Application.Emails.Commands.SendEmail.SendEmailCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewemail")]
        [RequirePermission(PermissionCodes.ViewEmail, PermissionLevels.Own)]
        public async Task<IActionResult> ViewEmail([FromQuery] PEMS.Application.Emails.Queries.ViewEmail.ViewEmailQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("replytoemail")]
        [RequirePermission(PermissionCodes.ReplyToEmail, PermissionLevels.Own)]
        public async Task<IActionResult> ReplytoEmail([FromBody] PEMS.Application.Emails.Commands.ReplytoEmail.ReplytoEmailCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}

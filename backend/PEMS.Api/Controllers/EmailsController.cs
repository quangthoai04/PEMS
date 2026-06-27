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
    [RoleAuthorize(
        EffectiveRole.StaffLeader, 
        EffectiveRole.Staff, 
        EffectiveRole.DepartmentLead, 
        EffectiveRole.Department, 
        EffectiveRole.Ho
    )]
    public class EmailsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public EmailsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewemaillist")]
        public async Task<IActionResult> ViewEmailList([FromQuery] PEMS.Application.Emails.Queries.ViewEmailList.ViewEmailListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

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

        [HttpPost("{id}/mark-completed")]
        public async Task<IActionResult> MarkCompleted(ulong id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Emails.Commands.MarkEmailCompleted.MarkEmailCompletedCommand { SentEmailId = id }, cancellationToken);
            return Ok(result);
        }

        [HttpGet("unprocessed-count")]
        public async Task<IActionResult> GetUnprocessedCount(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Emails.Queries.GetUnprocessedEmailCount.GetUnprocessedEmailCountQuery(), cancellationToken);
            return Ok(result);
        }

        // ── Editable email drafts / autosave (DB-backed, owner-scoped) ────────
        // Draft → Sent/Discarded; never hard-deleted. Recipients/attachments stored in
        // email_draft_recipients / email_draft_attachments; the handlers enforce owner + status rules.

        [HttpPost("drafts")]
        public async Task<IActionResult> CreateDraft([FromBody] PEMS.Application.Emails.Commands.CreateEmailDraft.CreateEmailDraftCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("drafts/{draftId}")]
        public async Task<IActionResult> GetDraft(ulong draftId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Emails.Queries.GetEmailDraft.GetEmailDraftQuery(draftId), cancellationToken);
            return Ok(result);
        }

        [HttpPut("drafts/{draftId}")]
        public async Task<IActionResult> UpdateDraft(ulong draftId, [FromBody] PEMS.Application.Emails.Commands.UpdateEmailDraft.UpdateEmailDraftCommand command, CancellationToken cancellationToken)
        {
            command.EmailDraftId = draftId; // route is authoritative
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPatch("drafts/{draftId}/discard")]
        public async Task<IActionResult> DiscardDraft(ulong draftId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Emails.Commands.DiscardEmailDraft.DiscardEmailDraftCommand(draftId), cancellationToken);
            return Ok(result);
        }

        [HttpPost("drafts/{draftId}/send")]
        public async Task<IActionResult> SendDraft(ulong draftId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Emails.Commands.SendEmailDraft.SendEmailDraftCommand(draftId), cancellationToken);
            return Ok(result);
        }
    }
}

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
        private readonly PEMS.Application.Emails.Common.EmailRecipientOptions _recipientOptions;

        public EmailsController(
            IMediator mediator,
            Microsoft.Extensions.Options.IOptions<PEMS.Application.Emails.Common.EmailRecipientOptions> recipientOptions)
        {
            _mediator = mediator;
            _recipientOptions = recipientOptions?.Value ?? new PEMS.Application.Emails.Common.EmailRecipientOptions();
        }

        /// <summary>
        /// The recipient ceiling the compose screen must respect, read from the same
        /// <c>EmailRecipientOptions</c> the validator uses.
        ///
        /// <para>
        /// It is served rather than duplicated on purpose. The options type says the limit lives in
        /// configuration "and nowhere else", because a literal copied into a frontend constant is exactly
        /// how the UI starts promising something the backend rejects. The screen shows "12/50" only
        /// because the server said 50.
        /// </para>
        /// </summary>
        [HttpGet("recipient-limits")]
        public IActionResult GetRecipientLimits()
            => Ok(new { maxRecipients = _recipientOptions.MaxRecipients });

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

        /// <summary>
        /// DEPRECATED duplicate of <c>PUT /api/email-templates/{id}</c>. Kept because removing a route is
        /// the owner's call, not this change's; no frontend or test calls it.
        ///
        /// <para>
        /// It was a live authorization bypass (G11-H audit). G11-I moved the template writes on
        /// <c>EmailTemplatesController</c> to <c>[RoleAuthorize(Ho)]</c> per PERMISSION_MATRIX §5.5, but
        /// this action inherited only the class-level attribute listing five roles — so Staff, Department
        /// and Staff Leader could still edit system template content here, through a route the screen
        /// never shows. Hiding a button had made no difference; a second door was standing open.
        /// </para>
        /// </summary>
        [HttpPost("updateemailtemplate")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> UpdateEmailTemplate([FromBody] PEMS.Application.Emails.Commands.UpdateEmailTemplate.UpdateEmailTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// DEPRECATED duplicate of <c>POST /api/email-templates</c> (UC-45, itself deprecated). The
        /// handler refuses unconditionally with <c>EMAIL_TEMPLATE_CATALOG_FIXED</c>; the attribute is
        /// here so a caller without the right is told THAT rather than being told the catalog is fixed,
        /// which would confirm the route still resolves.
        /// </summary>
        [HttpPost("createemailtemplate")]
        [RoleAuthorize(EffectiveRole.Ho)]
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

        /// <summary>
        /// Reply to the original sender. <c>ReplyAll</c> on the body is forced off here so this route has
        /// exactly one meaning — a client that wants Reply All asks for it by URL, not by a flag it might
        /// set by accident.
        /// </summary>
        [HttpPost("replytoemail")]
        public async Task<IActionResult> ReplytoEmail([FromBody] PEMS.Application.Emails.Commands.ReplytoEmail.ReplytoEmailCommand command, CancellationToken cancellationToken)
        {
            command.ReplyAll = false;
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Reply All: the original sender plus the original's VISIBLE recipients, minus the replier. The
        /// original's blind copies are never included — the handler does not read them, and the caller
        /// cannot supply them, because this body carries a mode and not an address list.
        /// </summary>
        [HttpPost("replyalltoemail")]
        public async Task<IActionResult> ReplyAllToEmail([FromBody] PEMS.Application.Emails.Commands.ReplytoEmail.ReplytoEmailCommand command, CancellationToken cancellationToken)
        {
            command.ReplyAll = true;
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

        /// <summary>
        /// The caller's own unsent drafts, for the "Nháp" tab. Ownership is enforced inside the
        /// handler, not by this controller's role attribute.
        /// </summary>
        [HttpGet("drafts")]
        public async Task<IActionResult> ListDrafts(
            [FromQuery] PEMS.Application.Emails.Queries.ListEmailDrafts.ListEmailDraftsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
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

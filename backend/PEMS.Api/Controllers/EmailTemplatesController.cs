using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Queries.PreviewEmailTemplate;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// The system email template catalog.
    ///
    /// <para>
    /// Authorization is per action, not per controller (G11-I). It used to be one
    /// <c>[RoleAuthorize]</c> on the class listing five roles, which is correct for the READ side —
    /// anyone composing an email picks a template and previews it — but meant Staff, Department and
    /// Staff Leader could also create, update and deactivate system templates. PERMISSION_MATRIX
    /// §5.5 gives UC-42 to UC-45 to HO alone, with every other role marked `—`.
    /// </para>
    /// </summary>
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

        /// <summary>
        /// What a template's variables actually are (G11-J): allowed, required, sensitive, whether a
        /// subject may carry them, whether CC/BCC are permitted, and a preview sample per variable.
        ///
        /// <para>
        /// Open to every composing role, not just HO: the compose screen needs
        /// <c>allowCc</c>/<c>allowBcc</c> to decide whether to offer those fields, and inferring it in
        /// the client from a template's name is exactly the guess this endpoint removes.
        /// </para>
        /// </summary>
        [HttpGet("contract/{templateCode}")]
        public async Task<IActionResult> GetContract(
            string templateCode, [FromQuery] string? language, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Emails.Queries.GetEmailTemplateContract.GetEmailTemplateContractQuery
                {
                    TemplateCode = templateCode,
                    Language = language,
                },
                cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// The reply-contact settings for one template.
        ///
        /// <para>
        /// Readable by every composing role for the same reason the contract is: the compose and preview
        /// screens need to know whether a contact block will appear. Writing them is HO only, like every
        /// other template write on this controller.
        /// </para>
        /// </summary>
        [HttpGet("{templateCode}/contact-settings")]
        public async Task<IActionResult> GetContactSettings(
            string templateCode, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Emails.Contact.GetEmailContactSettingsQuery
                {
                    TemplateCode = templateCode,
                },
                cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// Saves the reply-contact settings. HO only.
        ///
        /// <para>
        /// The payload carries enums and booleans plus two headings — never an address, a telephone
        /// number or a user id. Choosing WHICH fields the block shows is configuration; choosing what it
        /// SAYS would be a way to attribute a hand-typed mailbox to somebody else, so the command has no
        /// field for it.
        /// </para>
        /// </summary>
        [HttpPut("{templateCode}/contact-settings")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> UpdateContactSettings(
            string templateCode,
            [FromBody] PEMS.Application.Emails.Contact.UpdateEmailContactSettingsCommand command,
            CancellationToken cancellationToken)
        {
            command.TemplateCode = templateCode;
            var result = await _mediator.Send(command, cancellationToken);
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

        /// <summary>
        /// Kept only to answer with <c>EMAIL_TEMPLATE_CATALOG_FIXED</c> (UC-45 deprecated, G11-I). The
        /// handler refuses unconditionally; this attribute exists so an unauthorised caller is told it
        /// is unauthorised rather than being told the catalog is fixed, which would leak that the route
        /// still resolves.
        /// </summary>
        [HttpPost]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> Create([FromBody] PEMS.Application.Emails.Commands.CreateEmailTemplate.CreateEmailTemplateCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>UC-44 — HO only. Content fields only; see the command for what was removed.</summary>
        [HttpPut("{id}")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> Update(ulong id, [FromBody] PEMS.Application.Emails.Commands.UpdateEmailTemplate.UpdateEmailTemplateCommand command, CancellationToken cancellationToken)
        {
            command.EmailTemplateId = id;
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Puts a template's content back to the wording PEMS ships (G11-I). HO only, like every other
        /// write on this controller.
        ///
        /// <para>
        /// POST rather than PUT: it is not idempotent in the way that matters here — each call bumps the
        /// revision and writes an audit row recording what was replaced, and that record is the point.
        /// </para>
        /// </summary>
        [HttpPost("{id}/restore-default")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> RestoreDefault(
            ulong id,
            [FromBody] PEMS.Application.Emails.Commands.RestoreEmailTemplate.RestoreEmailTemplateCommand command,
            CancellationToken cancellationToken)
        {
            command.EmailTemplateId = id;
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>Kept only to answer with <c>EMAIL_TEMPLATE_CATALOG_FIXED</c> (G11-I).</summary>
        [HttpPatch("{id}/status")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> ToggleStatus(ulong id, [FromBody] PEMS.Application.Emails.Commands.ToggleEmailTemplateStatus.ToggleEmailTemplateStatusCommand command, CancellationToken cancellationToken)
        {
            command.EmailTemplateId = id;
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
    }
}

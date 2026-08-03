using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Partners.Aliases.Commands.CreatePartnerAlias;
using PEMS.Application.Partners.Aliases.Commands.DeactivatePartnerAlias;
using PEMS.Application.Partners.Aliases.Queries.GetPartnerAliases;
using PEMS.Application.Partners.Commands.ApprovePartner;
using PEMS.Application.Partners.Commands.CreatePartner;
using PEMS.Application.Partners.Commands.RejectPartner;
using PEMS.Application.Partners.Commands.TranslatePartnerDraft;
using PEMS.Application.Partners.Commands.UpdatePartner;
using PEMS.Application.Partners.Contacts.Commands.CreatePartnerContact;
using PEMS.Application.Partners.Contacts.Commands.DeactivatePartnerContact;
using PEMS.Application.Partners.Contacts.Commands.SetPrimaryPartnerContact;
using PEMS.Application.Partners.Contacts.Commands.UpdatePartnerContact;
using PEMS.Application.Partners.Contacts.Queries.GetPartnerContacts;
using PEMS.Application.Partners.Documents.Commands.UploadPartnerDocument;
using PEMS.Application.Partners.Documents.Queries.GetPartnerDocuments;
using PEMS.Application.Partners.Queries.GetPartnerDetail;
using PEMS.Application.Partners.Queries.GetPartnerVisitHistory;
using PEMS.Application.Partners.Queries.GetPartners;
using PEMS.Application.Partners.Queries.GetPendingPartnerApprovals;
using PEMS.Application.Partners.Queries.MatchPartner;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Partner Management (docs/PARTNER_canh/01). Authorization is fixed role/scope policy
    /// enforced inside the MediatR handlers (owner_campus_id / effective role) — controllers
    /// only route.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/partners")]
    public class PartnersController : ControllerBase
    {
        private readonly IMediator _mediator;
        public PartnersController(IMediator mediator) => _mediator = mediator;

        // ── Partner main ────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetPartners([FromQuery] GetPartnersQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        [HttpGet("pending-approvals")]
        public async Task<IActionResult> GetPendingApprovals([FromQuery] GetPendingPartnerApprovalsQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        [HttpGet("match")]
        public async Task<IActionResult> Match([FromQuery] MatchPartnerQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        [HttpGet("{partnerId}")]
        public async Task<IActionResult> GetPartnerDetail(ulong partnerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPartnerDetailQuery(partnerId), cancellationToken));

        [HttpGet("{partnerId}/visit-history")]
        public async Task<IActionResult> GetPartnerVisitHistory(ulong partnerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPartnerVisitHistoryQuery(partnerId), cancellationToken));

        [HttpPost]
        public async Task<IActionResult> CreatePartner([FromBody] CreatePartnerCommand command, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(command, cancellationToken));

        [HttpPut("{partnerId}")]
        public async Task<IActionResult> UpdatePartner(ulong partnerId, [FromBody] UpdatePartnerCommand command, CancellationToken cancellationToken)
        {
            command.PartnerId = partnerId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpPost("{partnerId}/approve")]
        public async Task<IActionResult> ApprovePartner(ulong partnerId, [FromBody] ApprovePartnerCommand command, CancellationToken cancellationToken)
        {
            command.PartnerId = partnerId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpPost("{partnerId}/reject")]
        public async Task<IActionResult> RejectPartner(ulong partnerId, [FromBody] RejectPartnerCommand command, CancellationToken cancellationToken)
        {
            command.PartnerId = partnerId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        // One-shot VI→EN preview for the "EN" button on the create/edit partner form; never
        // persists (mirrors POST /api/news/translate-draft).
        [HttpPost("translate-draft")]
        public async Task<IActionResult> TranslatePartnerDraft(
            [FromBody] TranslatePartnerDraftCommand command, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(command, cancellationToken));

        // ── Contacts ────────────────────────────────────────────────────

        [HttpGet("{partnerId}/contacts")]
        public async Task<IActionResult> GetContacts(ulong partnerId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPartnerContactsQuery(partnerId, includeInactive), cancellationToken));

        [HttpPost("{partnerId}/contacts")]
        public async Task<IActionResult> CreateContact(ulong partnerId, [FromBody] CreatePartnerContactCommand command, CancellationToken cancellationToken)
        {
            command.PartnerId = partnerId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpPut("{partnerId}/contacts/{contactId}")]
        public async Task<IActionResult> UpdateContact(ulong partnerId, ulong contactId, [FromBody] UpdatePartnerContactCommand command, CancellationToken cancellationToken)
        {
            command.PartnerId = partnerId;
            command.ContactId = contactId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpDelete("{partnerId}/contacts/{contactId}")]
        public async Task<IActionResult> DeactivateContact(ulong partnerId, ulong contactId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new DeactivatePartnerContactCommand(partnerId, contactId), cancellationToken));

        [HttpPost("{partnerId}/contacts/{contactId}/set-primary")]
        public async Task<IActionResult> SetPrimaryContact(ulong partnerId, ulong contactId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new SetPrimaryPartnerContactCommand(partnerId, contactId), cancellationToken));

        // ── Aliases ─────────────────────────────────────────────────────

        [HttpGet("{partnerId}/aliases")]
        public async Task<IActionResult> GetAliases(ulong partnerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPartnerAliasesQuery(partnerId), cancellationToken));

        [HttpPost("{partnerId}/aliases")]
        public async Task<IActionResult> CreateAlias(ulong partnerId, [FromBody] CreatePartnerAliasCommand command, CancellationToken cancellationToken)
        {
            command.PartnerId = partnerId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpDelete("{partnerId}/aliases/{aliasId}")]
        public async Task<IActionResult> DeactivateAlias(ulong partnerId, ulong aliasId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new DeactivatePartnerAliasCommand(partnerId, aliasId), cancellationToken));

        // ── Documents (documents.owner_type = PARTNER) ──────────────────

        [HttpGet("{partnerId}/documents")]
        public async Task<IActionResult> GetDocuments(ulong partnerId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPartnerDocumentsQuery(partnerId), cancellationToken));

        [HttpPost("{partnerId}/documents")]
        public async Task<IActionResult> UploadDocument(ulong partnerId, [FromBody] UploadPartnerDocumentCommand command, CancellationToken cancellationToken)
        {
            command.PartnerId = partnerId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }
    }
}

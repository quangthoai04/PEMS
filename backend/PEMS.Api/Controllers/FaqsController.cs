using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/faqs")]
    public sealed class FaqsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FaqsController(IMediator mediator) => _mediator = mediator;

        // UC-62: View List FAQ
        [HttpGet]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> GetFaqs(
            [FromQuery] PEMS.Application.Faqs.Queries.ViewListFAQ.ViewListFAQQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        // UC-64: View FAQ Detail
        [HttpGet("{faqId:long}")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> GetFaqDetail(
            [FromRoute] long faqId,
            CancellationToken cancellationToken)
        {
            var query = new PEMS.Application.Faqs.Queries.ViewFAQDetail.ViewFAQDetailQuery((ulong)faqId);
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        // UC-63: Create FAQ
        [HttpPost]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> CreateFAQ(
            [FromBody] PEMS.Application.Faqs.Commands.CreateFAQ.CreateFAQCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // UC-64: Update FAQ
        [HttpPut("{faqId:long}")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> UpdateFAQ(
            [FromRoute] long faqId,
            [FromBody] PEMS.Application.Faqs.Commands.UpdateFAQ.UpdateFAQBody body,
            CancellationToken cancellationToken)
        {
            var command = new PEMS.Application.Faqs.Commands.UpdateFAQ.UpdateFAQCommand(
                (ulong)faqId, body.FaqType, body.Question, body.Answer);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // ChangeFAQVisibility
        [HttpPatch("visibility")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> ChangeFAQVisibility(
            [FromBody] PEMS.Application.Faqs.Commands.ChangeFAQVisibility.ChangeFAQVisibilityCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
    }
}

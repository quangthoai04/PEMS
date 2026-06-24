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

        // UC-62: View List FAQ — chỉ HO
        [HttpGet]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> GetFaqs(
            [FromQuery] PEMS.Application.Faqs.Queries.ViewListFAQ.ViewListFAQQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> CreateFAQ(
            [FromBody] PEMS.Application.Faqs.Commands.CreateFAQ.CreateFAQCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPut]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> UpdateFAQ(
            [FromBody] PEMS.Application.Faqs.Commands.UpdateFAQ.UpdateFAQCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

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

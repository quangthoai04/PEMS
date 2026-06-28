using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/public")]
    public sealed class PublicContentController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PublicContentController(IMediator mediator) => _mediator = mediator;

        [HttpGet("homepage")]
        public async Task<IActionResult> ViewHomepage(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewHomepage.ViewHomepageQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchInformation(
            [FromQuery] PEMS.Application.PublicContent.Queries.SearchInformation.SearchInformationQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("contact")]
        public async Task<IActionResult> ViewContactInfo(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewContactInfo.ViewContactInfoQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("policy")]
        public async Task<IActionResult> ViewPolicyAndTerms(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewPolicyAndTerms.ViewPolicyAndTermsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("faqs")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFaqs(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("news")]
        public async Task<IActionResult> ViewNews(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewNews.ViewNewsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("partners")]
        public async Task<IActionResult> ViewPartners(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewPartners.ViewPartnersQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("gallery")]
        public async Task<IActionResult> ViewGallery(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewGallery.ViewGalleryQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("notifications")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ViewNotifications(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewNotifications.ViewNotificationsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPatch("notifications/{notificationId}/read")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> MarkNotificationRead(ulong notificationId, CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new PEMS.Application.PublicContent.Commands.MarkNotificationsRead.MarkNotificationsReadCommand(notificationId),
                cancellationToken);
            return NoContent();
        }

        [HttpPatch("notifications/read-all")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new PEMS.Application.PublicContent.Commands.MarkNotificationsRead.MarkNotificationsReadCommand(null),
                cancellationToken);
            return NoContent();
        }
    }
}

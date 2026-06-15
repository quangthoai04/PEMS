using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PublicContentController : ControllerBase
    {
        private readonly IMediator _mediator;
        public PublicContentController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewhomepage")]
        public async Task<IActionResult> ViewHomepage([FromQuery] PEMS.Application.PublicContent.Queries.ViewHomepage.ViewHomepageQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchinformation")]
        public async Task<IActionResult> SearchInformation([FromQuery] PEMS.Application.PublicContent.Queries.SearchInformation.SearchInformationQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewcontactinfo")]
        public async Task<IActionResult> ViewContactInfo([FromQuery] PEMS.Application.PublicContent.Queries.ViewContactInfo.ViewContactInfoQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewpolicyandterms")]
        public async Task<IActionResult> ViewPolicyAndTerms([FromQuery] PEMS.Application.PublicContent.Queries.ViewPolicyAndTerms.ViewPolicyAndTermsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewfaq")]
        public async Task<IActionResult> ViewFAQ([FromQuery] PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFAQQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewnews")]
        public async Task<IActionResult> ViewNews([FromQuery] PEMS.Application.PublicContent.Queries.ViewNews.ViewNewsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewpartners")]
        public async Task<IActionResult> ViewPartners([FromQuery] PEMS.Application.PublicContent.Queries.ViewPartners.ViewPartnersQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewgallery")]
        public async Task<IActionResult> ViewGallery([FromQuery] PEMS.Application.PublicContent.Queries.ViewGallery.ViewGalleryQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewnotifications")]
        public async Task<IActionResult> ViewNotifications([FromQuery] PEMS.Application.PublicContent.Queries.ViewNotifications.ViewNotificationsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}

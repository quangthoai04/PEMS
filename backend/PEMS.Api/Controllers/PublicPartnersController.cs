using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Partners.Queries.GetPublicPartnerDetail;
using PEMS.Application.Partners.Queries.GetPublicPartners;
using PEMS.Application.Partners.Queries.SearchPublicPartnerOptions;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers;

/// <summary>Public partner surface — only APPROVED + PUBLIC profiles ever leave this controller.</summary>
[ApiController]
[Route("api/public/partners")]
public sealed class PublicPartnersController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicPartnersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetPublicPartners(
        [FromQuery] GetPublicPartnersQuery query, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [AllowAnonymous]
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new SearchPublicPartnerOptionsQuery
            {
                Keyword = keyword,
                Limit = limit
            },
            cancellationToken);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{partnerIdOrSlug}")]
    public async Task<IActionResult> GetPublicPartnerDetail(
        string partnerIdOrSlug, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetPublicPartnerDetailQuery(partnerIdOrSlug), cancellationToken));
}

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Partners.Queries.GetPublicPartnerCountries;
using PEMS.Application.Partners.Queries.GetPublicPartnerDetail;
using PEMS.Application.Partners.Queries.GetPublicPartnerMedia;
using PEMS.Application.Partners.Queries.GetPublicPartnerTypes;
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

    /// <summary>Distinct countries among APPROVED + PUBLIC partners — for the list page's country
    /// filter dropdown, and to validate GlobeComponent's pin-click value before filtering by it.</summary>
    [AllowAnonymous]
    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetPublicPartnerCountriesQuery(), cancellationToken));

    /// <summary>Distinct partner_type values (with counts) among APPROVED + PUBLIC partners — for the
    /// list page's partner type filter.</summary>
    [AllowAnonymous]
    [HttpGet("types")]
    public async Task<IActionResult> GetPartnerTypes(
        [FromQuery] string? languageCode, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetPublicPartnerTypesQuery(languageCode), cancellationToken));

    [AllowAnonymous]
    [HttpGet("{partnerIdOrSlug}")]
    public async Task<IActionResult> GetPublicPartnerDetail(
        string partnerIdOrSlug, [FromQuery] string? languageCode, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetPublicPartnerDetailQuery(partnerIdOrSlug, languageCode), cancellationToken));

    /// <summary>
    /// Partner-scoped public file proxy (logo/cover) — inline, cacheable, no session needed. The public
    /// pages cannot use the authenticated <c>/api/files/{id}/content</c>, so this is the anonymous
    /// alternative (mirrors <c>PublicVisitFptuController.GetMediaContent</c>).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("media/{fileId:long}/content")]
    public async Task<IActionResult> GetMediaContent(long fileId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPublicPartnerMediaQuery((ulong)fileId), cancellationToken);
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(result.Content, result.ContentType);
    }
}

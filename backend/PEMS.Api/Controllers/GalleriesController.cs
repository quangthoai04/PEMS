using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GalleriesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public GalleriesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewgalleryitemlist")]
        public async Task<IActionResult> ViewGalleryItemList([FromQuery] PEMS.Application.Galleries.Queries.ViewGalleryItemList.ViewGalleryItemListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchgalleryitems")]
        public async Task<IActionResult> SearchGalleryItems([FromQuery] PEMS.Application.Galleries.Queries.SearchGalleryItems.SearchGalleryItemsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("addgalleryitem")]
        public async Task<IActionResult> AddGalleryItem([FromBody] PEMS.Application.Galleries.Commands.AddGalleryItem.AddGalleryItemCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updategalleryitem")]
        public async Task<IActionResult> UpdateGalleryItem([FromBody] PEMS.Application.Galleries.Commands.UpdateGalleryItem.UpdateGalleryItemCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("deletegalleryitem")]
        public async Task<IActionResult> DeleteGalleryItem([FromBody] PEMS.Application.Galleries.Commands.DeleteGalleryItem.DeleteGalleryItemCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Galleries.Tts.Commands.EnsurePublicGalleryItemTtsAudio;
using PEMS.Application.Galleries.Tts.Queries.GetPublicGalleryItemTtsAudioStatus;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Anonymous TTS narration endpoints behind the public gallery's speaker icon. "Ensure" lazily
    /// creates a generation job for the item's current description when no matching audio exists;
    /// the GET is the cheap poll the frontend loops on until READY. Only public-visible items are
    /// served (hidden/inactive → 404), and the returned audioUrl always points at the PEMS-hosted
    /// anonymous media proxy — never at EverAI.
    /// </summary>
    [ApiController]
    [Route("api/public/gallery-items")]
    [AllowAnonymous]
    public sealed class PublicGalleryTtsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PublicGalleryTtsController(IMediator mediator) => _mediator = mediator;

        // Speaker icon click: READY (+audioUrl) | PROCESSING | TEMPORARILY_UNAVAILABLE.
        [HttpPost("{galleryItemId:long}/tts-audio/ensure")]
        public async Task<IActionResult> EnsureTtsAudio(long galleryItemId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new EnsurePublicGalleryItemTtsAudioCommand(galleryItemId), cancellationToken));

        // Poll while PROCESSING: READY | PROCESSING | NOT_CREATED | TEMPORARILY_UNAVAILABLE | DISABLED | INVALID_DESCRIPTION.
        [HttpGet("{galleryItemId:long}/tts-audio")]
        public async Task<IActionResult> GetTtsAudioStatus(long galleryItemId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPublicGalleryItemTtsAudioStatusQuery(galleryItemId), cancellationToken));
    }
}

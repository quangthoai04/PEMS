using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Galleries.Tts.Commands.RegenerateGalleryItemTtsAudio;
using PEMS.Application.Galleries.Tts.Queries.GetGalleryItemTtsStatus;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Staff Leader TTS management. Like GalleriesController, the handler self-guards the role/campus
    /// scope (STAFF+LEADER, item in the caller's primary campus), so no [Authorize] attribute is needed
    /// here — anonymous callers get 403 from the scope guard.
    /// </summary>
    [ApiController]
    [Route("api/gallery-management")]
    public sealed class GalleryManagementTtsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public GalleryManagementTtsController(IMediator mediator) => _mediator = mediator;

        // Audio status badge for the detail modal (READY / PROCESSING / FAILED / STALE / NOT_CREATED …).
        [HttpGet("items/{galleryItemId:long}/tts-audio")]
        public async Task<IActionResult> GetTtsAudioStatus(long galleryItemId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetGalleryItemTtsStatusQuery(galleryItemId), cancellationToken));

        // "Tạo lại audio": force a fresh MANUAL_REGENERATE job (bypasses the failed cooldown). No-op
        // (UP_TO_DATE) when the current description already has matching READY audio.
        [HttpPost("items/{galleryItemId:long}/tts-audio/regenerate")]
        public async Task<IActionResult> RegenerateTtsAudio(long galleryItemId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new RegenerateGalleryItemTtsAudioCommand(galleryItemId), cancellationToken));
    }
}

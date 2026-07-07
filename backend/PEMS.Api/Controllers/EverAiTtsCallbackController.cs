using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PEMS.Application.Galleries.Tts;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Webhook receiver for EverAI TTS results (used when <c>EverAiTts:UseCallback</c> is true; in
    /// polling mode EverAI simply never calls it). Always answers 200 — handled, duplicate (idempotent)
    /// and unknown request ids alike — so EverAI never retries into an error loop. Payload contents are
    /// logged sanitized (request id only, never the audio link or any secret).
    /// </summary>
    [ApiController]
    [Route("api/integrations/everai/tts")]
    [AllowAnonymous]
    public sealed class EverAiTtsCallbackController : ControllerBase
    {
        private readonly IGalleryItemTtsService _tts;
        private readonly ILogger<EverAiTtsCallbackController> _logger;

        public EverAiTtsCallbackController(
            IGalleryItemTtsService tts, ILogger<EverAiTtsCallbackController> logger)
        {
            _tts = tts;
            _logger = logger;
        }

        [HttpPost("callback")]
        public async Task<IActionResult> Callback(
            [FromBody] EverAiTtsCallbackDto callback, CancellationToken cancellationToken)
        {
            try
            {
                await _tts.HandleEverAiCallbackAsync(callback, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Still 200: the polling sweep will finish the job; failing the webhook only makes
                // EverAI hammer us with retries.
                _logger.LogError(ex, "EverAI TTS callback for request {RequestId} failed to process.",
                    callback?.RequestId);
            }

            return Ok(new { success = true });
        }
    }
}

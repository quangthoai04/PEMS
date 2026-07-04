using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.BusinessCardOcr.Commands.ConfirmBusinessCardContact;
using PEMS.Application.BusinessCardOcr.Commands.DiscardBusinessCardOcrJob;
using PEMS.Application.BusinessCardOcr.Commands.ScanBusinessCard;
using PEMS.Application.BusinessCardOcr.Queries.GetBusinessCardOcrJob;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Business-card OCR (docs/PARTNER_canh/02 §8). The scan returns a DRAFT the user
    /// must review; only confirm-contact writes partner_contacts.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/business-card-ocr")]
    public class BusinessCardOcrController : ControllerBase
    {
        private readonly IMediator _mediator;
        public BusinessCardOcrController(IMediator mediator) => _mediator = mediator;

        [HttpPost("scan")]
        [RequestSizeLimit(15 * 1024 * 1024)] // hard cap above the 10 MB business limit
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Scan(
            IFormFile file,
            [FromForm] ulong? visitInstanceId,
            [FromForm] ulong? guestMemberId,
            [FromForm] ulong? minuteParticipantId,
            [FromForm] ulong? partnerId,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Tệp danh thiếp rỗng hoặc không hợp lệ." });

            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);

            var result = await _mediator.Send(new ScanBusinessCardCommand
            {
                FileBytes = ms.ToArray(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                VisitInstanceId = visitInstanceId,
                GuestMemberId = guestMemberId,
                MinuteParticipantId = minuteParticipantId,
                PartnerId = partnerId,
                IdempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var key)
                    ? key.ToString() : null,
                ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            }, cancellationToken);

            return Ok(result);
        }

        [HttpGet("jobs/{ocrJobId}")]
        public async Task<IActionResult> GetJob(ulong ocrJobId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetBusinessCardOcrJobQuery(ocrJobId), cancellationToken));

        [HttpPost("jobs/{ocrJobId}/confirm-contact")]
        public async Task<IActionResult> ConfirmContact(ulong ocrJobId,
            [FromBody] ConfirmBusinessCardContactCommand command, CancellationToken cancellationToken)
        {
            command.OcrJobId = ocrJobId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpPost("jobs/{ocrJobId}/discard")]
        public async Task<IActionResult> Discard(ulong ocrJobId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new DiscardBusinessCardOcrJobCommand(ocrJobId), cancellationToken));
    }
}

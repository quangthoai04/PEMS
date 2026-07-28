using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.VisitDocuments.Commands.UploadVisitDocument;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// "Tài liệu" upload widget of a visit-in-progress (VisitDuringTab). Authorization lives in the
    /// handler (<see cref="Application.Delegations.VisitPhotos.VisitInstanceMediaAccessScope"/>) —
    /// the controller only maps transport.
    /// </summary>
    [ApiController]
    [Route("api/visit-documents")]
    public class VisitDocumentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public VisitDocumentsController(IMediator mediator) => _mediator = mediator;

        // Single-file upload; 0..N partner names tag the same file to several partners at once, or
        // save it as OwnerType=VISIT ("Theo đoàn khách") when no partner is selected.
        [HttpPost("instances/{visitInstanceId}/upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(15 * 1024 * 1024)] // 10 MB file + form overhead
        public async Task<IActionResult> Upload(
            ulong visitInstanceId, IFormFile file, [FromForm] string title,
            [FromForm] List<string>? partnerNames, CancellationToken cancellationToken)
        {
            var command = new UploadVisitDocumentCommand
            {
                VisitInstanceId = visitInstanceId,
                Title = title,
                PartnerNames = partnerNames ?? new List<string>(),
            };

            if (file != null && file.Length > 0)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);
                command.Content = ms.ToArray();
                command.FileName = file.FileName;
                command.ContentType = file.ContentType;
            }

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
    }
}

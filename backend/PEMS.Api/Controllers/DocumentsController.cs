using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public DocumentsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewdocumentlist")]
        public async Task<IActionResult> ViewDocumentList([FromQuery] PEMS.Application.Documents.Queries.ViewDocumentList.ViewDocumentListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchdocuments")]
        public async Task<IActionResult> SearchDocuments([FromQuery] PEMS.Application.Documents.Queries.SearchDocuments.SearchDocumentsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{documentId}")]
        public async Task<IActionResult> ViewDocumentDetail(ulong documentId, CancellationToken cancellationToken)
        {
            var query = new PEMS.Application.Documents.Queries.ViewDocumentDetail.ViewDocumentDetailQuery { DocumentId = documentId };
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
    }
}

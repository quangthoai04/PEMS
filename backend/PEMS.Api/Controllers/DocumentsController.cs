using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Document list/search (UC-55, UC-56). The controller carried no authorization attribute
    /// at all, so the document list was readable without a token.
    ///
    /// Roles are Staff Leader, Staff and HO. PERMISSION_MATRIX §5.7 marks HO as `—`, but HO
    /// uses this screen today and has always had it on the menu, so the matrix is the side
    /// that needs updating — cutting a live role's access is not something this change should
    /// do silently. Per-document visibility still runs in the handlers /
    /// FileAccessAuthorizationService: this gate only decides who may ask.
    /// </summary>
    [ApiController]
    [Authorize]
    [RoleAuthorize(EffectiveRole.Ho, EffectiveRole.StaffLeader, EffectiveRole.Staff)]
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

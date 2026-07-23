using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.ApiIntegrations.Commands.SetApiIntegrationStatus;
using PEMS.Application.ApiIntegrations.Commands.TestApiIntegration;
using PEMS.Application.ApiIntegrations.Commands.UpdateApiIntegrationQuota;
using PEMS.Application.ApiIntegrations.Commands.UpsertGoogleDocumentAiOcrConfig;
using PEMS.Application.ApiIntegrations.Commands.UpsertGoogleTranslationConfig;
using PEMS.Application.ApiIntegrations.Commands.UpsertGoogleVisionFaceDetectionConfig;
using PEMS.Application.ApiIntegrations.Queries.GetApiIntegrationDetail;
using PEMS.Application.ApiIntegrations.Queries.GetApiIntegrationLogs;
using PEMS.Application.ApiIntegrations.Queries.GetApiIntegrationQuota;
using PEMS.Application.ApiIntegrations.Queries.GetApiIntegrations;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// API Configuration admin surface (docs/PARTNER_canh/02 §6.1). ADMIN manages,
    /// HO reads — enforced inside the handlers. Secrets are never echoed back.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/api-integrations")]
    public class ApiIntegrationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IWebHostEnvironment _environment;

        public ApiIntegrationsController(IMediator mediator, IWebHostEnvironment environment)
        {
            _mediator = mediator;
            _environment = environment;
        }

        /// <summary>Detailed test diagnostics only surface in Development/Testing environments.</summary>
        private bool ShowTestDiagnostics
            => _environment.IsDevelopment() || _environment.IsEnvironment("Testing");

        private PEMS.Application.ApiIntegrations.Common.ApiIntegrationDto Sanitize(
            PEMS.Application.ApiIntegrations.Common.ApiIntegrationDto dto)
        {
            if (!ShowTestDiagnostics) dto.LastTestMessage = null;
            return dto;
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] GetApiIntegrationsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result.Select(Sanitize).ToList());
        }

        [HttpGet("{apiConfigId}")]
        public async Task<IActionResult> GetDetail(ulong apiConfigId, CancellationToken cancellationToken)
            => Ok(Sanitize(await _mediator.Send(new GetApiIntegrationDetailQuery(apiConfigId), cancellationToken)));

        /// <summary>Create/replace the Google Document AI business-card OCR config.</summary>
        [HttpPost("business-card-ocr/google-document-ai")]
        public async Task<IActionResult> UpsertGoogleDocumentAiConfig(
            [FromBody] UpsertGoogleDocumentAiOcrConfigCommand command, CancellationToken cancellationToken)
        {
            command.ApiConfigId = null;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpPut("{apiConfigId}")]
        public async Task<IActionResult> Update(ulong apiConfigId,
            [FromBody] UpsertGoogleDocumentAiOcrConfigCommand command, CancellationToken cancellationToken)
        {
            command.ApiConfigId = apiConfigId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        /// <summary>
        /// Create-or-update (by api_code) the Google Cloud Translation config for News
        /// auto-translation. Used for both create and edit — there is a single well-known row.
        /// </summary>
        [HttpPost("news-translation/google-cloud-translation")]
        public async Task<IActionResult> UpsertGoogleTranslationConfig(
            [FromBody] UpsertGoogleTranslationConfigCommand command, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(command, cancellationToken));

        /// <summary>
        /// Create-or-update (by api_code) the Google Cloud Vision config for Face Detection.
        /// Used for both create and edit — there is a single well-known row.
        /// </summary>
        [HttpPost("face-detection/google-cloud-vision")]
        public async Task<IActionResult> UpsertGoogleVisionFaceDetectionConfig(
            [FromBody] UpsertGoogleVisionFaceDetectionConfigCommand command, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(command, cancellationToken));

        [HttpPost("{apiConfigId}/test")]
        public async Task<IActionResult> Test(ulong apiConfigId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new TestApiIntegrationCommand(apiConfigId), cancellationToken));

        [HttpPost("{apiConfigId}/enable")]
        public async Task<IActionResult> Enable(ulong apiConfigId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new SetApiIntegrationStatusCommand(apiConfigId, true), cancellationToken));

        [HttpPost("{apiConfigId}/disable")]
        public async Task<IActionResult> Disable(ulong apiConfigId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new SetApiIntegrationStatusCommand(apiConfigId, false), cancellationToken));

        [HttpGet("{apiConfigId}/logs")]
        public async Task<IActionResult> GetLogs(ulong apiConfigId, [FromQuery] GetApiIntegrationLogsQuery query, CancellationToken cancellationToken)
        {
            query.ApiConfigId = apiConfigId;
            return Ok(await _mediator.Send(query, cancellationToken));
        }

        [HttpGet("{apiConfigId}/quota")]
        public async Task<IActionResult> GetQuota(ulong apiConfigId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetApiIntegrationQuotaQuery(apiConfigId), cancellationToken));

        [HttpPut("{apiConfigId}/quota")]
        public async Task<IActionResult> UpdateQuota(ulong apiConfigId,
            [FromBody] UpdateApiIntegrationQuotaCommand command, CancellationToken cancellationToken)
        {
            command.ApiConfigId = apiConfigId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }
    }
}

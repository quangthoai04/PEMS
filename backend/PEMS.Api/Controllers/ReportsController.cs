using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.Reports.Commands.ExportHoReport;
using PEMS.Application.Reports.Queries.GetHoReportOverview;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // No report endpoint may be called anonymously.
    public class ReportsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ReportsController(IMediator mediator) => _mediator = mediator;

        /// <summary>Head Office system-wide report (KPI, approval funnel, campus performance, close readiness…).</summary>
        [HttpGet("ho-overview")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> GetHoOverview([FromQuery] GetHoReportOverviewQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Exports the HO report (PDF/EXCEL/CSV) using the exact filters currently applied.</summary>
        [HttpPost("ho-overview/export")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> ExportHoReport([FromBody] ExportHoReportCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return File(result.Content, result.ContentType, result.FileName);
        }

        [HttpGet("viewdashboardstatistics")]
        public async Task<IActionResult> ViewDashboardStatistics([FromQuery] PEMS.Application.Reports.Queries.ViewDashboardStatistics.ViewDashboardStatisticsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("exportstatisticsreport")]
        public async Task<IActionResult> ExportStatisticsReport([FromBody] PEMS.Application.Reports.Commands.ExportStatisticsReport.ExportStatisticsReportCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("filterdashboardbytime")]
        public async Task<IActionResult> FilterDashboardByTime([FromQuery] PEMS.Application.Reports.Queries.FilterDashboardByTime.FilterDashboardByTimeQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}

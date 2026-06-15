using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ReportsController(IMediator mediator) => _mediator = mediator;

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

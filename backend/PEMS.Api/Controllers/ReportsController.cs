using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.Reports.Commands.ExportHoReport;
using PEMS.Application.Reports.Queries.GetHoReportOverview;
using PEMS.Application.Reports.Queries.GetStaffLeaderReportOverview;
using PEMS.Application.Reports.Commands.ExportStaffLeaderReport;
using PEMS.Application.Reports.Queries.GetDeptLeaderReportOverview;
using PEMS.Application.Reports.Commands.ExportDeptLeaderReport;
using PEMS.Application.Reports.Queries.GetDeptLeaderInvoiceData;
using PEMS.Application.Reports.Commands.ExportDeptLeaderInvoice;
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

        [HttpGet("staff-leader-overview")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> GetStaffLeaderOverview([FromQuery] GetStaffLeaderReportOverviewQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("staff-leader-overview/export")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> ExportStaffLeaderReport([FromBody] ExportStaffLeaderReportCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return File(result.Content, result.ContentType, result.FileName);
        }

        /// <summary>Department Leader operation report — scoped to the leader's own department.</summary>
        [HttpGet("department-leader-overview")]
        [RoleAuthorize(EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> GetDeptLeaderOverview([FromQuery] GetDeptLeaderReportOverviewQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Exports the Department Leader report (PDF/EXCEL/CSV) using the exact filters currently applied.</summary>
        [HttpPost("department-leader-overview/export")]
        [RoleAuthorize(EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> ExportDeptLeaderReport([FromBody] ExportDeptLeaderReportCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return File(result.Content, result.ContentType, result.FileName);
        }

        /// <summary>Visits with logistics items requested to the leader's department (invoice tab).</summary>
        [HttpGet("department-leader-invoice/visits")]
        [RoleAuthorize(EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> GetDeptLeaderInvoiceVisits([FromQuery] GetDeptLeaderInvoiceVisitsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Logistics items the host asked the department to prepare for one visit (invoice tab).</summary>
        [HttpGet("department-leader-invoice/visits/{visitInstanceId}/items")]
        [RoleAuthorize(EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> GetDeptLeaderInvoiceItems(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetDeptLeaderInvoiceItemsQuery { VisitInstanceId = visitInstanceId }, cancellationToken);
            return Ok(result);
        }

        /// <summary>Generates and downloads the logistics preparation invoice PDF (nothing persisted).</summary>
        [HttpPost("department-leader-invoice/export-pdf")]
        [RoleAuthorize(EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> ExportDeptLeaderInvoicePdf([FromBody] ExportDeptLeaderInvoiceCommand command, CancellationToken cancellationToken)
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

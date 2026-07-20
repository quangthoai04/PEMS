using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.Reports.Commands.ExportHoReport;
using PEMS.Application.Reports.Queries.GetHoReportOverview;
using PEMS.Application.Reports.Queries.GetStaffLeaderReportOverview;
using PEMS.Application.Reports.Queries.GetStaffLeaderReportV2;
using PEMS.Application.Reports.Commands.ExportStaffLeaderReport;
using PEMS.Application.Reports.Commands.SendStaffLeaderPersonnelReport;
using PEMS.Application.Reports.Commands.SendStaffLeaderDepartmentReport;
using PEMS.Application.Reports.Commands.ExportStaffLeaderReportV2;
using PEMS.Application.Reports.Queries.GetHoReportV2;
using PEMS.Application.Reports.Commands.SendHoCampusReport;
using PEMS.Application.Reports.Commands.ExportHoReportV2;
using PEMS.Application.Reports.Queries.GetDeptLeaderReportV2;
using PEMS.Application.Reports.Commands.SendDeptLeaderPersonnelReport;

using PEMS.Application.Reports.Commands.ExportDeptLeaderReportV2;
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

        /// <summary>Báo cáo hệ thống 3 phần của HO (tổng quan + campus, đối tác) — lọc theo thời gian.</summary>
        [HttpGet("ho-report-v2")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> GetHoReportV2([FromQuery] GetHoReportV2Query query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Gửi email báo cáo vận hành 1 campus cho Staff Leader campus đó (HO).</summary>
        [HttpPost("ho-report-v2/send-campus-report")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> SendHoCampusReport([FromBody] SendHoCampusReportCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>Xuất báo cáo hệ thống HO (PDF/Excel/CSV) — chọn phần tổng quan/đối tác hoặc cả hai.</summary>
        [HttpPost("ho-report-v2/export")]
        [RoleAuthorize(EffectiveRole.Ho)]
        public async Task<IActionResult> ExportHoReportV2([FromBody] ExportHoReportV2Command command, CancellationToken cancellationToken)
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

        /// <summary>Báo cáo campus 3 phần của Staff Leader (đoàn khách, nhân sự, phòng ban) — lọc theo thời gian.</summary>
        [HttpGet("staff-leader-report-v2")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> GetStaffLeaderReportV2([FromQuery] GetStaffLeaderReportV2Query query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Xuất báo cáo campus 3 phần (PDF/Excel/CSV) — chọn phần 1/2/3 hoặc tất cả.</summary>
        [HttpPost("staff-leader-report-v2/export")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> ExportStaffLeaderReportV2([FromBody] ExportStaffLeaderReportV2Command command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return File(result.Content, result.ContentType, result.FileName);
        }

        /// <summary>Đơn hậu cần phòng ban đã nhận trong khoảng ngày — panel xuất hóa đơn (Staff Leader).</summary>
        [HttpGet("staff-leader-report-v2/departments/{departmentId}/invoice-items")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> GetStaffLeaderDeptInvoiceItems(
            ulong departmentId, [FromQuery] GetStaffLeaderDeptInvoiceItemsQuery query, CancellationToken cancellationToken)
        {
            query.DepartmentId = departmentId;
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Gửi email báo cáo hiệu suất cá nhân cho 1 nhân sự/student (Staff Leader).</summary>
        [HttpPost("staff-leader-report-v2/send-personnel-report")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> SendStaffLeaderPersonnelReport(
            [FromBody] SendStaffLeaderPersonnelReportCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>Gửi email báo cáo phối hợp tiếp khách cho trưởng 1 phòng ban (Staff Leader).</summary>
        [HttpPost("staff-leader-report-v2/send-department-report")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> SendStaffLeaderDepartmentReport(
            [FromBody] SendStaffLeaderDepartmentReportCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>Thống kê chi phí các đoàn trong khoảng ngày — panel phần 4 trang báo cáo (Staff Leader).</summary>
        [HttpGet("staff-leader-report-v2/expense-visits")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> GetStaffLeaderExpenseVisits(
            [FromQuery] GetStaffLeaderExpenseVisitsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Báo cáo phòng ban 2 phần của Department Leader (nhiệm vụ + nhân sự) — lọc theo thời gian.</summary>
        [HttpGet("dept-leader-report-v2")]
        [RoleAuthorize(EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> GetDeptLeaderReportV2([FromQuery] GetDeptLeaderReportV2Query query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Đơn hậu cần phòng ban ĐÃ HOÀN THÀNH trong khoảng ngày — panel xuất hóa đơn (Dept Leader).</summary>
        [HttpGet("dept-leader-report-v2/invoice-items")]
        [RoleAuthorize(EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> GetDeptLeaderInvoiceItemsV2([FromQuery] GetDeptLeaderInvoiceItemsV2Query query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Gửi email báo cáo hiệu suất cá nhân cho 1 nhân sự phòng ban (Dept Leader).</summary>
        [HttpPost("dept-leader-report-v2/send-personnel-report")]
        [RoleAuthorize(EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> SendDeptLeaderPersonnelReport(
            [FromBody] SendDeptLeaderPersonnelReportCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>Xuất báo cáo phòng ban (PDF/Excel/CSV) — chọn phần nhiệm vụ/nhân sự hoặc cả hai.</summary>
        [HttpPost("dept-leader-report-v2/export")]
        [RoleAuthorize(EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> ExportDeptLeaderReportV2([FromBody] ExportDeptLeaderReportV2Command command, CancellationToken cancellationToken)
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

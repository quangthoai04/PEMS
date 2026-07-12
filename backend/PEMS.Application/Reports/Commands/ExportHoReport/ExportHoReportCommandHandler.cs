using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Reports.Queries.GetHoReportOverview;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using PEMS.Application.Common;
namespace PEMS.Application.Reports.Commands.ExportHoReport;

/// <summary>
/// Builds the HO report file (CSV / Excel via ClosedXML / PDF via QuestPDF) from the same
/// aggregation as the dashboard, so the export always matches the filters on screen.
/// </summary>
public sealed class ExportHoReportCommandHandler : IRequestHandler<ExportHoReportCommand, ExportHoReportResult>
{
    private const string BrandBlue = "#004C91";

    private static readonly string[] AllSections =
    {
        "EXECUTIVE_SUMMARY",
        "APPROVAL_OVERVIEW",
        "CAMPUS_PERFORMANCE",
        "LIFECYCLE_CLOSE_READINESS",
        "FEEDBACK_QUALITY",
        "CONTENT_EMAIL_EFFECTIVENESS",
        "PARTNER_ENGAGEMENT",
    };

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ExportHoReportCommandHandler(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ExportHoReportResult> Handle(ExportHoReportCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(_currentUser.RoleCode, "HO", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền xuất báo cáo Head Office.");

        var overview = await _mediator.Send(new GetHoReportOverviewQuery
        {
            Preset = request.Preset,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            CampusId = request.CampusId,
            VisitScope = request.VisitScope,
            RequestStatus = request.RequestStatus,
            CampusInstanceStatus = request.CampusInstanceStatus,
            VisitType = request.VisitType,
        }, cancellationToken);

        var sections = request.ReportSections is { Count: > 0 }
            ? request.ReportSections.Select(s => s.Trim().ToUpperInvariant()).Where(s => AllSections.Contains(s)).ToHashSet()
            : AllSections.ToHashSet();

        var format = request.ExportFormat?.Trim().ToUpperInvariant() switch
        {
            "PDF" => "PDF",
            "CSV" => "CSV",
            _ => "EXCEL",
        };

        var stampVn = VietnamTime.Now();
        var baseName = $"PEMS_HO_Report_{stampVn:yyyyMMdd_HHmm}";

        return format switch
        {
            "CSV" => new ExportHoReportResult
            {
                Content = BuildCsv(overview, sections),
                ContentType = "text/csv; charset=utf-8",
                FileName = baseName + ".csv",
            },
            "PDF" => new ExportHoReportResult
            {
                Content = BuildPdf(overview, sections),
                ContentType = "application/pdf",
                FileName = baseName + ".pdf",
            },
            _ => new ExportHoReportResult
            {
                Content = BuildExcel(overview, sections),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = baseName + ".xlsx",
            },
        };
    }

    // ─────────────────────────── Shared label helpers ───────────────────────────

    private static string PresetLabel(string preset) => preset switch
    {
        "THIS_MONTH" => "Tháng này",
        "THIS_QUARTER" => "Quý này",
        "CUSTOM" => "Tùy chỉnh",
        _ => "Năm nay",
    };

    private static string ScopeLabel(string scope) => scope switch
    {
        "SINGLE_CAMPUS" => "Single-campus",
        "MULTI_CAMPUS" => "Multi-campus",
        _ => "Tất cả",
    };

    private static string Dt(DateTime? d) => d?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—";
    private static string Num(double? v) => v?.ToString("0.#", CultureInfo.InvariantCulture) ?? "—";

    private static List<(string Label, string Value)> HeaderLines(HoReportOverviewDto o) => new()
    {
        ("Hệ thống", "Partnership Engagement Management System"),
        ("Báo cáo", "Head Office Report"),
        ("Khoảng thời gian", $"{PresetLabel(o.FilterSummary.Preset)} ({Dt(o.FilterSummary.FromDate)} – {Dt(o.FilterSummary.ToDate)})"),
        ("Cơ sở", o.FilterSummary.CampusName),
        ("Phạm vi chuyến", ScopeLabel(o.FilterSummary.VisitScope)),
        ("Trạng thái đơn", o.FilterSummary.RequestStatus),
        ("Trạng thái instance", o.FilterSummary.CampusInstanceStatus),
        ("Loại chuyến", o.FilterSummary.VisitType),
        ("Người xuất", o.FilterSummary.GeneratedByName ?? "—"),
        ("Thời điểm xuất", o.GeneratedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (GMT+7)"),
    };

    private static List<(string Label, string Value)> KpiLines(HoReportKpisDto k) => new()
    {
        ("Tổng yêu cầu trong kỳ", k.TotalRequests.ToString()),
        ("Đơn multi-campus đang chờ duyệt", k.MultiCampusPending.ToString()),
        ("Đã duyệt", k.ApprovedRequests.ToString()),
        ("Bị từ chối", k.RejectedRequests.ToString()),
        ("Đã hủy", k.CancelledRequests.ToString()),
        ("Campus instance đang xử lý", k.ActiveCampusInstances.ToString()),
        ("Đã đóng hồ sơ", k.ClosedCampusInstances.ToString()),
        ("Quá hạn đóng hồ sơ", k.OverdueCloseInstances.ToString()),
        ("Thời gian quyết định TB (giờ)", Num(k.AverageDecisionHours)),
        ("Điểm feedback TB", Num(k.AverageFeedbackRating)),
        ("Tổng lượt khách", k.TotalGuests.ToString()),
    };

    // ─────────────────────────────────── CSV ───────────────────────────────────

    private static byte[] BuildCsv(HoReportOverviewDto o, HashSet<string> sections)
    {
        var sb = new StringBuilder();
        void Row(params object?[] cells) =>
            sb.AppendLine(string.Join(",", cells.Select(c =>
            {
                var s = Convert.ToString(c, CultureInfo.InvariantCulture) ?? "";
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            })));

        foreach (var (label, value) in HeaderLines(o)) Row(label, value);
        Row();

        if (sections.Contains("EXECUTIVE_SUMMARY"))
        {
            Row("1. EXECUTIVE SUMMARY");
            foreach (var (label, value) in KpiLines(o.Kpis)) Row(label, value);
            Row();
            Row("Cần chú ý", "Số lượng", "Mức độ", "Mô tả");
            foreach (var a in o.AttentionItems) Row(a.Label, a.Count, a.Severity, a.Description);
            Row();
        }

        if (sections.Contains("APPROVAL_OVERVIEW"))
        {
            Row("2. APPROVAL & REQUEST OVERVIEW");
            Row("Đã duyệt", o.ApprovalBreakdown.Approved);
            Row("Bị từ chối", o.ApprovalBreakdown.Rejected);
            Row("Đang chờ", o.ApprovalBreakdown.Pending);
            Row("Đã hủy", o.ApprovalBreakdown.Cancelled);
            Row("Tỷ lệ duyệt (%)", o.ApprovalBreakdown.ApprovalRate);
            Row("Tỷ lệ từ chối (%)", o.ApprovalBreakdown.RejectionRate);
            Row("Thời gian quyết định TB (giờ)", Num(o.ApprovalBreakdown.AverageDecisionHours));
            Row();
            Row("Tháng", "Tổng yêu cầu", "Single-campus", "Multi-campus", "Đã duyệt", "Từ chối", "Đã hủy", "Lượt khách");
            foreach (var m in o.MonthlyTrend)
                Row(m.MonthLabel, m.TotalRequests, m.SingleCampusRequests, m.MultiCampusRequests, m.Approved, m.Rejected, m.Cancelled, m.TotalGuests);
            Row();
            Row("Đơn liên cơ sở đang chờ HO xử lý", $"Top {o.MultiCampusPendingRequests.Count}/{o.MultiCampusPendingTotal}");
            Row("Mã đơn", "Tên đoàn", "Tổ chức", "Số campus", "Số khách", "Ngày thăm", "Giờ chờ", "Trạng thái");
            foreach (var p in o.MultiCampusPendingRequests)
                Row(p.RequestCode, p.DelegationName, p.OrganizationName, p.RequestedCampusCount, p.GuestCount,
                    $"{Dt(p.PlannedStartAt)} – {Dt(p.PlannedEndAt)}", p.WaitingHours, p.Status);
            Row();
        }

        if (sections.Contains("CAMPUS_PERFORMANCE"))
        {
            Row("3. CAMPUS PERFORMANCE");
            Row("Campus", "Tổng chuyến", "Chờ xử lý", "Từ chối", "Đã gán", "Trước tiếp", "Đang tiếp", "Sau tiếp", "Đã đóng", "Đã hủy", "Quá hạn", "Lượt khách", "Feedback TB");
            foreach (var c in o.CampusPerformance)
                Row(c.CampusName, c.TotalInstances, c.WaitingRequestApproval, c.Rejected, c.Assigned,
                    c.BeforeVisit, c.DuringVisit, c.AfterVisit, c.Closed, c.Cancelled, c.OverdueCloseCount, c.GuestCount, Num(c.AverageFeedbackRating));
            Row();
        }

        if (sections.Contains("LIFECYCLE_CLOSE_READINESS"))
        {
            Row("4. LIFECYCLE & CLOSE READINESS");
            Row("Trạng thái", "Số lượng", "Tỷ lệ (%)");
            foreach (var s in o.LifecyclePipeline) Row(s.LabelVi, s.Count, s.Percentage);
            Row();
            Row("Hồ sơ sau tiếp khách cần hoàn tất", $"Top {o.CloseReadiness.Count}/{o.CloseReadinessTotal}");
            Row("Mã đơn", "Đoàn", "Campus", "Host", "Kết thúc", "Logistics mở", "Bàn giao thiếu ký", "Việc biên bản mở", "Biên bản", "News", "Feedback", "Có thể đóng", "Vướng mắc");
            foreach (var c in o.CloseReadiness)
                Row(c.RequestCode, c.DelegationName, c.CampusName, c.HostName ?? "—", Dt(c.PlannedEndAt),
                    c.LogisticsOpenCount, c.MissingHandoverSignatureCount, c.OpenActionItemCount,
                    c.HasMinutes ? "Có" : "Chưa", c.NewsNotRequired ? "Không cần" : (c.HasPublishedNews ? "Đã đăng" : "Thiếu"),
                    c.FeedbackCount, c.CanClose ? "Có" : "Chưa", string.Join("; ", c.Blockers));
            Row();
        }

        if (sections.Contains("FEEDBACK_QUALITY"))
        {
            Row("5. FEEDBACK QUALITY");
            Row("Điểm trung bình", Num(o.FeedbackSummary.AverageRating));
            Row("Tổng feedback", o.FeedbackSummary.TotalFeedbacks);
            Row("Feedback thấp (≤2)", o.FeedbackSummary.LowFeedbackCount);
            Row();
            Row("Đánh giá theo campus", "Điểm TB", "Số feedback");
            foreach (var c in o.FeedbackSummary.RatingByCampus) Row(c.CampusName, c.AverageRating, c.FeedbackCount);
            Row();
            Row("Top đánh giá cao", "Campus", "Điểm TB", "Số feedback");
            foreach (var v in o.FeedbackSummary.TopRatedVisits) Row(v.DelegationName, v.CampusName, v.AverageRating, v.FeedbackCount);
            Row("Top đánh giá thấp", "Campus", "Điểm TB", "Số feedback");
            foreach (var v in o.FeedbackSummary.LowRatedVisits) Row(v.DelegationName, v.CampusName, v.AverageRating, v.FeedbackCount);
            Row();
        }

        if (sections.Contains("CONTENT_EMAIL_EFFECTIVENESS"))
        {
            var ce = o.ContentAndEmailSummary;
            Row("6. CONTENT & EMAIL EFFECTIVENESS");
            Row("News đã đăng trong kỳ", ce.PublishedNewsCount);
            Row("News chờ duyệt", ce.PendingNewsCount);
            Row("Instance thiếu news", ce.InstancesMissingNewsCount);
            Row("Email gửi thành công", ce.EmailSentCount);
            Row("Email lỗi", ce.EmailFailedCount);
            Row("Tỷ lệ gửi thành công (%)", Num(ce.EmailDeliveredRate));
            Row("Action token đã phản hồi", ce.ActionTokenRespondedCount);
            Row("Action token hết hạn", ce.ActionTokenExpiredCount);
            Row("Action token đang chờ", ce.ActionTokenPendingCount);
            Row();
        }

        if (sections.Contains("PARTNER_ENGAGEMENT"))
        {
            var ps = o.PartnerSummary;
            Row("7. PARTNER ENGAGEMENT");
            Row("Tổng partner (đã duyệt)", ps.TotalPartners);
            Row("Đang hợp tác (ACTIVE)", ps.ActivePartners);
            Row("Hồ sơ chờ duyệt", ps.PendingApprovalPartners);
            Row("Partner mới trong kỳ", ps.NewPartnersInPeriod);
            Row("Chuyến trong kỳ có partner", ps.VisitsWithPartner);
            Row();
            Row("Phân bổ theo loại", "Số lượng");
            if (ps.PartnersByType.Count == 0) Row("No data available for this section");
            else foreach (var t in ps.PartnersByType) Row(t.PartnerType, t.Count);
            Row();
            Row("Partner theo campus", "Đã duyệt", "Chờ duyệt", "Mới trong kỳ");
            if (ps.PartnersByCampus.Count == 0) Row("No data available for this section");
            else foreach (var c in ps.PartnersByCampus) Row(c.CampusName, c.ApprovedCount, c.PendingCount, c.NewInPeriod);
            Row();
            Row("Top partner theo số chuyến", "Loại", "Quốc gia", "Campus quản lý", "Hợp tác", "Số chuyến", "Khách gắn partner");
            if (ps.TopPartners.Count == 0) Row("No data available for this section");
            else foreach (var p in ps.TopPartners)
                Row(p.Name, p.PartnerType, p.Country ?? "—", p.OwnerCampusName, p.CooperationStatus, p.VisitCount, p.LinkedGuestCount);
        }

        // UTF-8 BOM so Excel opens Vietnamese text correctly.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // ────────────────────────────────── Excel ──────────────────────────────────

    private static byte[] BuildExcel(HoReportOverviewDto o, HashSet<string> sections)
    {
        using var workbook = new XLWorkbook();

        void StyleHeader(IXLWorksheet ws, int row, int colCount)
        {
            var range = ws.Range(row, 1, row, colCount);
            range.Style.Fill.BackgroundColor = XLColor.FromHtml(BrandBlue);
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Font.Bold = true;
        }

        // Sheet 1: overview (always present so the file always has context).
        var wsInfo = workbook.Worksheets.Add("Tổng quan");
        var r = 1;
        foreach (var (label, value) in HeaderLines(o))
        {
            wsInfo.Cell(r, 1).Value = label;
            wsInfo.Cell(r, 1).Style.Font.Bold = true;
            wsInfo.Cell(r, 2).Value = value;
            r++;
        }

        if (sections.Contains("EXECUTIVE_SUMMARY"))
        {
            r++;
            wsInfo.Cell(r, 1).Value = "KPI";
            wsInfo.Cell(r, 2).Value = "Giá trị";
            StyleHeader(wsInfo, r, 2);
            r++;
            foreach (var (label, value) in KpiLines(o.Kpis))
            {
                wsInfo.Cell(r, 1).Value = label;
                wsInfo.Cell(r, 2).Value = value;
                r++;
            }
            r++;
            wsInfo.Cell(r, 1).Value = "Cần chú ý";
            wsInfo.Cell(r, 2).Value = "Số lượng";
            wsInfo.Cell(r, 3).Value = "Mức độ";
            wsInfo.Cell(r, 4).Value = "Mô tả";
            StyleHeader(wsInfo, r, 4);
            r++;
            foreach (var a in o.AttentionItems)
            {
                wsInfo.Cell(r, 1).Value = a.Label;
                wsInfo.Cell(r, 2).Value = a.Count;
                wsInfo.Cell(r, 3).Value = a.Severity;
                wsInfo.Cell(r, 4).Value = a.Description;
                r++;
            }
        }
        wsInfo.Columns().AdjustToContents(1, 60);

        if (sections.Contains("APPROVAL_OVERVIEW"))
        {
            var ws = workbook.Worksheets.Add("Phê duyệt");
            ws.Cell(1, 1).Value = "Chỉ số";
            ws.Cell(1, 2).Value = "Giá trị";
            StyleHeader(ws, 1, 2);
            var rows = new (string, XLCellValue)[]
            {
                ("Đã duyệt", o.ApprovalBreakdown.Approved),
                ("Bị từ chối", o.ApprovalBreakdown.Rejected),
                ("Đang chờ", o.ApprovalBreakdown.Pending),
                ("Đã hủy", o.ApprovalBreakdown.Cancelled),
                ("Tỷ lệ duyệt (%)", o.ApprovalBreakdown.ApprovalRate),
                ("Tỷ lệ từ chối (%)", o.ApprovalBreakdown.RejectionRate),
                ("Thời gian quyết định TB (giờ)", Num(o.ApprovalBreakdown.AverageDecisionHours)),
            };
            for (var i = 0; i < rows.Length; i++)
            {
                ws.Cell(i + 2, 1).Value = rows[i].Item1;
                ws.Cell(i + 2, 2).Value = rows[i].Item2;
            }

            var tr = rows.Length + 3;
            string[] trendHeader = { "Tháng", "Tổng yêu cầu", "Single-campus", "Multi-campus", "Đã duyệt", "Từ chối", "Đã hủy", "Lượt khách" };
            for (var c = 0; c < trendHeader.Length; c++) ws.Cell(tr, c + 1).Value = trendHeader[c];
            StyleHeader(ws, tr, trendHeader.Length);
            tr++;
            foreach (var m in o.MonthlyTrend)
            {
                ws.Cell(tr, 1).Value = m.MonthLabel;
                ws.Cell(tr, 2).Value = m.TotalRequests;
                ws.Cell(tr, 3).Value = m.SingleCampusRequests;
                ws.Cell(tr, 4).Value = m.MultiCampusRequests;
                ws.Cell(tr, 5).Value = m.Approved;
                ws.Cell(tr, 6).Value = m.Rejected;
                ws.Cell(tr, 7).Value = m.Cancelled;
                ws.Cell(tr, 8).Value = m.TotalGuests;
                tr++;
            }

            tr++;
            ws.Cell(tr, 1).Value = $"Đơn liên cơ sở đang chờ HO xử lý (top {o.MultiCampusPendingRequests.Count}/{o.MultiCampusPendingTotal})";
            ws.Cell(tr, 1).Style.Font.Bold = true;
            tr++;
            string[] pendingHeader = { "Mã đơn", "Tên đoàn", "Tổ chức", "Số campus", "Số khách", "Ngày thăm", "Giờ chờ", "Trạng thái" };
            for (var c = 0; c < pendingHeader.Length; c++) ws.Cell(tr, c + 1).Value = pendingHeader[c];
            StyleHeader(ws, tr, pendingHeader.Length);
            tr++;
            foreach (var p in o.MultiCampusPendingRequests)
            {
                ws.Cell(tr, 1).Value = p.RequestCode;
                ws.Cell(tr, 2).Value = p.DelegationName;
                ws.Cell(tr, 3).Value = p.OrganizationName;
                ws.Cell(tr, 4).Value = p.RequestedCampusCount;
                ws.Cell(tr, 5).Value = p.GuestCount;
                ws.Cell(tr, 6).Value = $"{Dt(p.PlannedStartAt)} – {Dt(p.PlannedEndAt)}";
                ws.Cell(tr, 7).Value = p.WaitingHours;
                ws.Cell(tr, 8).Value = p.Status;
                tr++;
            }
            ws.Columns().AdjustToContents(1, 60);
        }

        if (sections.Contains("CAMPUS_PERFORMANCE"))
        {
            var ws = workbook.Worksheets.Add("Hiệu suất cơ sở");
            string[] header = { "Campus", "Tổng chuyến", "Chờ duyệt", "Chờ host", "Đã gán", "Trước tiếp", "Đang tiếp", "Sau tiếp", "Đã đóng", "Đã hủy", "Quá hạn", "Lượt khách", "Feedback TB" };
            for (var c = 0; c < header.Length; c++) ws.Cell(1, c + 1).Value = header[c];
            StyleHeader(ws, 1, header.Length);
            var cr = 2;
            foreach (var cp in o.CampusPerformance)
            {
                ws.Cell(cr, 1).Value = cp.CampusName;
                ws.Cell(cr, 2).Value = cp.TotalInstances;
                ws.Cell(cr, 3).Value = cp.WaitingRequestApproval;
                ws.Cell(cr, 4).Value = cp.Rejected;
                ws.Cell(cr, 5).Value = cp.Assigned;
                ws.Cell(cr, 6).Value = cp.BeforeVisit;
                ws.Cell(cr, 7).Value = cp.DuringVisit;
                ws.Cell(cr, 8).Value = cp.AfterVisit;
                ws.Cell(cr, 9).Value = cp.Closed;
                ws.Cell(cr, 10).Value = cp.Cancelled;
                ws.Cell(cr, 11).Value = cp.OverdueCloseCount;
                ws.Cell(cr, 12).Value = cp.GuestCount;
                ws.Cell(cr, 13).Value = Num(cp.AverageFeedbackRating);
                cr++;
            }
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("LIFECYCLE_CLOSE_READINESS"))
        {
            var ws = workbook.Worksheets.Add("Đóng hồ sơ");
            ws.Cell(1, 1).Value = "Trạng thái";
            ws.Cell(1, 2).Value = "Số lượng";
            ws.Cell(1, 3).Value = "Tỷ lệ (%)";
            StyleHeader(ws, 1, 3);
            var lr = 2;
            foreach (var s in o.LifecyclePipeline)
            {
                ws.Cell(lr, 1).Value = s.LabelVi;
                ws.Cell(lr, 2).Value = s.Count;
                ws.Cell(lr, 3).Value = s.Percentage;
                lr++;
            }

            lr++;
            ws.Cell(lr, 1).Value = $"Hồ sơ sau tiếp khách cần hoàn tất (top {o.CloseReadiness.Count}/{o.CloseReadinessTotal})";
            ws.Cell(lr, 1).Style.Font.Bold = true;
            lr++;
            string[] header = { "Mã đơn", "Đoàn", "Campus", "Host", "Kết thúc", "Logistics mở", "Bàn giao thiếu ký", "Việc biên bản mở", "Biên bản", "News", "Feedback", "Có thể đóng", "Vướng mắc" };
            for (var c = 0; c < header.Length; c++) ws.Cell(lr, c + 1).Value = header[c];
            StyleHeader(ws, lr, header.Length);
            lr++;
            foreach (var cRow in o.CloseReadiness)
            {
                ws.Cell(lr, 1).Value = cRow.RequestCode;
                ws.Cell(lr, 2).Value = cRow.DelegationName;
                ws.Cell(lr, 3).Value = cRow.CampusName;
                ws.Cell(lr, 4).Value = cRow.HostName ?? "—";
                ws.Cell(lr, 5).Value = Dt(cRow.PlannedEndAt);
                ws.Cell(lr, 6).Value = cRow.LogisticsOpenCount;
                ws.Cell(lr, 7).Value = cRow.MissingHandoverSignatureCount;
                ws.Cell(lr, 8).Value = cRow.OpenActionItemCount;
                ws.Cell(lr, 9).Value = cRow.HasMinutes ? "Có" : "Chưa";
                ws.Cell(lr, 10).Value = cRow.NewsNotRequired ? "Không cần" : (cRow.HasPublishedNews ? "Đã đăng" : "Thiếu");
                ws.Cell(lr, 11).Value = cRow.FeedbackCount;
                ws.Cell(lr, 12).Value = cRow.CanClose ? "Có" : "Chưa";
                ws.Cell(lr, 13).Value = string.Join("; ", cRow.Blockers);
                lr++;
            }
            ws.Columns().AdjustToContents(1, 60);
        }

        if (sections.Contains("FEEDBACK_QUALITY"))
        {
            var ws = workbook.Worksheets.Add("Feedback");
            ws.Cell(1, 1).Value = "Chỉ số";
            ws.Cell(1, 2).Value = "Giá trị";
            StyleHeader(ws, 1, 2);
            ws.Cell(2, 1).Value = "Điểm trung bình";
            ws.Cell(2, 2).Value = Num(o.FeedbackSummary.AverageRating);
            ws.Cell(3, 1).Value = "Tổng feedback";
            ws.Cell(3, 2).Value = o.FeedbackSummary.TotalFeedbacks;
            ws.Cell(4, 1).Value = "Feedback thấp (≤2)";
            ws.Cell(4, 2).Value = o.FeedbackSummary.LowFeedbackCount;

            var fr = 6;
            ws.Cell(fr, 1).Value = "Campus";
            ws.Cell(fr, 2).Value = "Điểm TB";
            ws.Cell(fr, 3).Value = "Số feedback";
            StyleHeader(ws, fr, 3);
            fr++;
            foreach (var c in o.FeedbackSummary.RatingByCampus)
            {
                ws.Cell(fr, 1).Value = c.CampusName;
                ws.Cell(fr, 2).Value = c.AverageRating;
                ws.Cell(fr, 3).Value = c.FeedbackCount;
                fr++;
            }

            fr++;
            ws.Cell(fr, 1).Value = "Đoàn (đánh giá cao nhất)";
            ws.Cell(fr, 2).Value = "Campus";
            ws.Cell(fr, 3).Value = "Điểm TB";
            ws.Cell(fr, 4).Value = "Số feedback";
            StyleHeader(ws, fr, 4);
            fr++;
            foreach (var v in o.FeedbackSummary.TopRatedVisits)
            {
                ws.Cell(fr, 1).Value = v.DelegationName;
                ws.Cell(fr, 2).Value = v.CampusName;
                ws.Cell(fr, 3).Value = v.AverageRating;
                ws.Cell(fr, 4).Value = v.FeedbackCount;
                fr++;
            }
            fr++;
            ws.Cell(fr, 1).Value = "Đoàn (đánh giá thấp nhất)";
            ws.Cell(fr, 2).Value = "Campus";
            ws.Cell(fr, 3).Value = "Điểm TB";
            ws.Cell(fr, 4).Value = "Số feedback";
            StyleHeader(ws, fr, 4);
            fr++;
            foreach (var v in o.FeedbackSummary.LowRatedVisits)
            {
                ws.Cell(fr, 1).Value = v.DelegationName;
                ws.Cell(fr, 2).Value = v.CampusName;
                ws.Cell(fr, 3).Value = v.AverageRating;
                ws.Cell(fr, 4).Value = v.FeedbackCount;
                fr++;
            }
            ws.Columns().AdjustToContents(1, 60);
        }

        if (sections.Contains("CONTENT_EMAIL_EFFECTIVENESS"))
        {
            var ws = workbook.Worksheets.Add("News & Email");
            ws.Cell(1, 1).Value = "Chỉ số";
            ws.Cell(1, 2).Value = "Giá trị";
            StyleHeader(ws, 1, 2);
            var ce = o.ContentAndEmailSummary;
            var rows = new (string, XLCellValue)[]
            {
                ("News đã đăng trong kỳ", ce.PublishedNewsCount),
                ("News chờ duyệt", ce.PendingNewsCount),
                ("Instance thiếu news", ce.InstancesMissingNewsCount),
                ("Email gửi thành công", ce.EmailSentCount),
                ("Email lỗi", ce.EmailFailedCount),
                ("Tỷ lệ gửi thành công (%)", Num(ce.EmailDeliveredRate)),
                ("Action token đã phản hồi", ce.ActionTokenRespondedCount),
                ("Action token hết hạn", ce.ActionTokenExpiredCount),
                ("Action token đang chờ", ce.ActionTokenPendingCount),
            };
            for (var i = 0; i < rows.Length; i++)
            {
                ws.Cell(i + 2, 1).Value = rows[i].Item1;
                ws.Cell(i + 2, 2).Value = rows[i].Item2;
            }
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("PARTNER_ENGAGEMENT"))
        {
            var ps = o.PartnerSummary;
            var ws = workbook.Worksheets.Add("Partner");
            ws.Cell(1, 1).Value = "Chỉ số";
            ws.Cell(1, 2).Value = "Giá trị";
            StyleHeader(ws, 1, 2);
            var kpiRows = new (string, XLCellValue)[]
            {
                ("Tổng partner (đã duyệt)", ps.TotalPartners),
                ("Đang hợp tác (ACTIVE)", ps.ActivePartners),
                ("Hồ sơ chờ duyệt", ps.PendingApprovalPartners),
                ("Partner mới trong kỳ", ps.NewPartnersInPeriod),
                ("Chuyến trong kỳ có partner", ps.VisitsWithPartner),
            };
            for (var i = 0; i < kpiRows.Length; i++)
            {
                ws.Cell(i + 2, 1).Value = kpiRows[i].Item1;
                ws.Cell(i + 2, 2).Value = kpiRows[i].Item2;
            }

            var pr = kpiRows.Length + 3;
            ws.Cell(pr, 1).Value = "Loại partner";
            ws.Cell(pr, 2).Value = "Số lượng";
            StyleHeader(ws, pr, 2);
            pr++;
            foreach (var t in ps.PartnersByType)
            {
                ws.Cell(pr, 1).Value = t.PartnerType;
                ws.Cell(pr, 2).Value = t.Count;
                pr++;
            }

            pr++;
            string[] campusHeader = { "Campus", "Đã duyệt", "Chờ duyệt", "Mới trong kỳ" };
            for (var c = 0; c < campusHeader.Length; c++) ws.Cell(pr, c + 1).Value = campusHeader[c];
            StyleHeader(ws, pr, campusHeader.Length);
            pr++;
            foreach (var cRow in ps.PartnersByCampus)
            {
                ws.Cell(pr, 1).Value = cRow.CampusName;
                ws.Cell(pr, 2).Value = cRow.ApprovedCount;
                ws.Cell(pr, 3).Value = cRow.PendingCount;
                ws.Cell(pr, 4).Value = cRow.NewInPeriod;
                pr++;
            }

            pr++;
            string[] topHeader = { "Partner", "Loại", "Quốc gia", "Campus quản lý", "Hợp tác", "Số chuyến", "Khách gắn partner" };
            for (var c = 0; c < topHeader.Length; c++) ws.Cell(pr, c + 1).Value = topHeader[c];
            StyleHeader(ws, pr, topHeader.Length);
            pr++;
            foreach (var p in ps.TopPartners)
            {
                ws.Cell(pr, 1).Value = p.Name;
                ws.Cell(pr, 2).Value = p.PartnerType;
                ws.Cell(pr, 3).Value = p.Country ?? "—";
                ws.Cell(pr, 4).Value = p.OwnerCampusName;
                ws.Cell(pr, 5).Value = p.CooperationStatus;
                ws.Cell(pr, 6).Value = p.VisitCount;
                ws.Cell(pr, 7).Value = p.LinkedGuestCount;
                pr++;
            }
            ws.Columns().AdjustToContents(1, 60);
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─────────────────────────────────── PDF ───────────────────────────────────

    private static byte[] BuildPdf(HoReportOverviewDto o, HashSet<string> sections)
    {
        var headerBg = BrandBlue;
        var border = Colors.Grey.Lighten2;
        var muted = Colors.Grey.Darken1;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial").FontColor(Colors.Black));

                page.Header().Column(header =>
                {
                    header.Item().Text("Partnership Engagement Management System")
                        .FontSize(10).FontColor(muted);
                    header.Item().Text("HEAD OFFICE REPORT")
                        .FontSize(16).Bold().FontColor(headerBg);
                    header.Item().PaddingTop(4).Text(t =>
                    {
                        t.Span($"Kỳ: {PresetLabel(o.FilterSummary.Preset)} ({Dt(o.FilterSummary.FromDate)} – {Dt(o.FilterSummary.ToDate)})   ");
                        t.Span($"Cơ sở: {o.FilterSummary.CampusName}   ");
                        t.Span($"Phạm vi: {ScopeLabel(o.FilterSummary.VisitScope)}");
                    });
                    header.Item().Text($"Người xuất: {o.FilterSummary.GeneratedByName ?? "—"} · {o.GeneratedAt:dd/MM/yyyy HH:mm} (GMT+7)")
                        .FontSize(8).FontColor(muted);
                    header.Item().PaddingTop(6).LineHorizontal(1).LineColor(headerBg);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    void SectionTitle(string title)
                    {
                        col.Item().PaddingTop(10).Text(title).FontSize(11).Bold().FontColor(headerBg);
                        col.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(border);
                    }

                    void KeyValueTable(IEnumerable<(string Label, string Value)> rows)
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                            });
                            foreach (var (label, value) in rows)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor(border).Padding(3).Text(label);
                                table.Cell().BorderBottom(0.5f).BorderColor(border).Padding(3).Text(value).Bold();
                            }
                        });
                    }

                    void DataTable(string[] headerCells, IEnumerable<string[]> rows, float[]? weights = null)
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                for (var i = 0; i < headerCells.Length; i++)
                                    c.RelativeColumn(weights != null ? weights[i] : 1);
                            });
                            table.Header(h =>
                            {
                                foreach (var cell in headerCells)
                                    h.Cell().Background(headerBg).Padding(3)
                                        .Text(cell).FontColor(Colors.White).FontSize(8).Bold();
                            });
                            foreach (var row in rows)
                            {
                                foreach (var cell in row)
                                    table.Cell().BorderBottom(0.5f).BorderColor(border).Padding(3)
                                        .Text(cell).FontSize(8);
                            }
                        });
                    }

                    if (sections.Contains("EXECUTIVE_SUMMARY"))
                    {
                        SectionTitle("1. Executive Summary");
                        KeyValueTable(KpiLines(o.Kpis));
                        col.Item().PaddingTop(6);
                        DataTable(
                            new[] { "Cần chú ý", "Số lượng", "Mức độ" },
                            o.AttentionItems.Select(a => new[] { a.Label, a.Count.ToString(), a.Severity }),
                            new[] { 4f, 1f, 1.2f });
                    }

                    if (sections.Contains("APPROVAL_OVERVIEW"))
                    {
                        SectionTitle("2. Approval & Request Overview");
                        KeyValueTable(new List<(string, string)>
                        {
                            ("Đã duyệt", o.ApprovalBreakdown.Approved.ToString()),
                            ("Bị từ chối", o.ApprovalBreakdown.Rejected.ToString()),
                            ("Đang chờ", o.ApprovalBreakdown.Pending.ToString()),
                            ("Đã hủy", o.ApprovalBreakdown.Cancelled.ToString()),
                            ("Tỷ lệ duyệt", $"{o.ApprovalBreakdown.ApprovalRate}%"),
                            ("Thời gian quyết định TB", $"{Num(o.ApprovalBreakdown.AverageDecisionHours)} giờ"),
                        });
                        col.Item().PaddingTop(6);
                        DataTable(
                            new[] { "Tháng", "Tổng", "Single", "Multi", "Duyệt", "Từ chối", "Hủy", "Khách" },
                            o.MonthlyTrend.Select(m => new[]
                            {
                                m.MonthLabel, m.TotalRequests.ToString(), m.SingleCampusRequests.ToString(),
                                m.MultiCampusRequests.ToString(), m.Approved.ToString(), m.Rejected.ToString(),
                                m.Cancelled.ToString(), m.TotalGuests.ToString(),
                            }));
                        col.Item().PaddingTop(6).Text($"Đơn liên cơ sở đang chờ HO xử lý (top {o.MultiCampusPendingRequests.Count}/{o.MultiCampusPendingTotal})").FontSize(9).Bold();
                        col.Item().PaddingTop(2);
                        DataTable(
                            new[] { "Mã đơn", "Đoàn", "Tổ chức", "Campus", "Khách", "Ngày thăm", "Giờ chờ" },
                            o.MultiCampusPendingRequests.Select(p => new[]
                            {
                                p.RequestCode, p.DelegationName, p.OrganizationName,
                                p.RequestedCampusCount.ToString(), p.GuestCount.ToString(),
                                $"{Dt(p.PlannedStartAt)} – {Dt(p.PlannedEndAt)}",
                                p.WaitingHours.ToString("0", CultureInfo.InvariantCulture),
                            }),
                            new[] { 1.4f, 2f, 2f, 0.9f, 0.8f, 2f, 0.9f });
                    }

                    if (sections.Contains("CAMPUS_PERFORMANCE"))
                    {
                        SectionTitle("3. Campus Performance");
                        DataTable(
                            new[] { "Campus", "Tổng", "Từ chối", "Chuẩn bị", "Đang tiếp", "Sau tiếp", "Đóng", "Quá hạn", "Khách", "FB TB" },
                            o.CampusPerformance.Select(c => new[]
                            {
                                c.CampusName, c.TotalInstances.ToString(), c.Rejected.ToString(),
                                (c.Assigned + c.BeforeVisit).ToString(), c.DuringVisit.ToString(), c.AfterVisit.ToString(),
                                c.Closed.ToString(), c.OverdueCloseCount.ToString(), c.GuestCount.ToString(),
                                Num(c.AverageFeedbackRating),
                            }),
                            new[] { 2.4f, 0.8f, 1f, 1f, 1f, 1f, 0.8f, 1f, 0.8f, 0.8f });
                    }

                    if (sections.Contains("LIFECYCLE_CLOSE_READINESS"))
                    {
                        SectionTitle("4. Lifecycle & Close Readiness");
                        DataTable(
                            new[] { "Trạng thái", "Số lượng", "Tỷ lệ" },
                            o.LifecyclePipeline.Select(s => new[] { s.LabelVi, s.Count.ToString(), $"{s.Percentage}%" }),
                            new[] { 3f, 1f, 1f });
                        col.Item().PaddingTop(6).Text($"Hồ sơ sau tiếp khách cần hoàn tất (top {o.CloseReadiness.Count}/{o.CloseReadinessTotal})").FontSize(9).Bold();
                        col.Item().PaddingTop(2);
                        DataTable(
                            new[] { "Đoàn", "Campus", "Host", "Kết thúc", "Logistics", "Biên bản", "News", "FB", "Đóng được" },
                            o.CloseReadiness.Select(c => new[]
                            {
                                c.DelegationName, c.CampusName, c.HostName ?? "—", Dt(c.PlannedEndAt),
                                c.LogisticsOpenCount == 0 && c.MissingHandoverSignatureCount == 0 ? "Đủ" : "Còn mở",
                                c.HasMinutes ? (c.OpenActionItemCount == 0 ? "Đủ" : "Việc mở") : "Thiếu",
                                c.NewsNotRequired ? "Không cần" : (c.HasPublishedNews ? "Đã đăng" : "Thiếu"),
                                c.FeedbackCount.ToString(),
                                c.CanClose ? "Có" : "Chưa",
                            }),
                            new[] { 2.2f, 1.4f, 1.6f, 1.2f, 1.1f, 1.1f, 1.1f, 0.6f, 1f });
                    }

                    if (sections.Contains("FEEDBACK_QUALITY"))
                    {
                        SectionTitle("5. Feedback Quality");
                        KeyValueTable(new List<(string, string)>
                        {
                            ("Điểm trung bình", Num(o.FeedbackSummary.AverageRating)),
                            ("Tổng feedback", o.FeedbackSummary.TotalFeedbacks.ToString()),
                            ("Feedback thấp (≤2)", o.FeedbackSummary.LowFeedbackCount.ToString()),
                        });
                        if (o.FeedbackSummary.RatingByCampus.Count > 0)
                        {
                            col.Item().PaddingTop(6);
                            DataTable(
                                new[] { "Campus", "Điểm TB", "Số feedback" },
                                o.FeedbackSummary.RatingByCampus.Select(c => new[]
                                {
                                    c.CampusName, Num(c.AverageRating), c.FeedbackCount.ToString(),
                                }),
                                new[] { 3f, 1f, 1f });
                        }
                    }

                    if (sections.Contains("CONTENT_EMAIL_EFFECTIVENESS"))
                    {
                        var ce = o.ContentAndEmailSummary;
                        SectionTitle("6. Content & Email Effectiveness");
                        KeyValueTable(new List<(string, string)>
                        {
                            ("News đã đăng trong kỳ", ce.PublishedNewsCount.ToString()),
                            ("News chờ duyệt", ce.PendingNewsCount.ToString()),
                            ("Instance thiếu news", ce.InstancesMissingNewsCount.ToString()),
                            ("Email gửi thành công", ce.EmailSentCount.ToString()),
                            ("Email lỗi", ce.EmailFailedCount.ToString()),
                            ("Tỷ lệ gửi thành công", ce.EmailDeliveredRate != null ? $"{ce.EmailDeliveredRate}%" : "—"),
                            ("Action token đã phản hồi", ce.ActionTokenRespondedCount.ToString()),
                            ("Action token hết hạn", ce.ActionTokenExpiredCount.ToString()),
                            ("Action token đang chờ", ce.ActionTokenPendingCount.ToString()),
                        });
                    }

                    if (sections.Contains("PARTNER_ENGAGEMENT"))
                    {
                        var ps = o.PartnerSummary;
                        SectionTitle("7. Partner Engagement");
                        KeyValueTable(new List<(string, string)>
                        {
                            ("Tổng partner (đã duyệt)", ps.TotalPartners.ToString()),
                            ("Đang hợp tác (ACTIVE)", ps.ActivePartners.ToString()),
                            ("Hồ sơ chờ duyệt", ps.PendingApprovalPartners.ToString()),
                            ("Partner mới trong kỳ", ps.NewPartnersInPeriod.ToString()),
                            ("Chuyến trong kỳ có partner", ps.VisitsWithPartner.ToString()),
                        });
                        if (ps.PartnersByCampus.Count > 0)
                        {
                            col.Item().PaddingTop(6).Text("Partner theo campus").FontSize(9).Bold();
                            col.Item().PaddingTop(2);
                            DataTable(
                                new[] { "Campus", "Đã duyệt", "Chờ duyệt", "Mới trong kỳ" },
                                ps.PartnersByCampus.Select(c => new[]
                                {
                                    c.CampusName, c.ApprovedCount.ToString(), c.PendingCount.ToString(), c.NewInPeriod.ToString(),
                                }),
                                new[] { 3f, 1f, 1f, 1.2f });
                        }
                        if (ps.TopPartners.Count > 0)
                        {
                            col.Item().PaddingTop(6).Text("Top partner theo số chuyến trong kỳ").FontSize(9).Bold();
                            col.Item().PaddingTop(2);
                            DataTable(
                                new[] { "Partner", "Loại", "Quốc gia", "Campus", "Chuyến", "Khách" },
                                ps.TopPartners.Select(p => new[]
                                {
                                    p.Name, p.PartnerType, p.Country ?? "—", p.OwnerCampusName,
                                    p.VisitCount.ToString(), p.LinkedGuestCount.ToString(),
                                }),
                                new[] { 2.8f, 1.2f, 1.1f, 1.6f, 0.7f, 0.7f });
                        }
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(muted));
                    t.Span("PEMS · Head Office Report · Trang ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}

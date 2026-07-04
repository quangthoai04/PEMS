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
using PEMS.Application.Reports.Queries.GetStaffLeaderReportOverview;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Reports.Commands.ExportStaffLeaderReport;

/// <summary>
/// Builds the Staff Leader campus report file (CSV / Excel via ClosedXML / PDF via QuestPDF)
/// from the same aggregation as the dashboard (GetStaffLeaderReportOverviewQuery), so the
/// export always matches the filters on screen.
/// </summary>
public sealed class ExportStaffLeaderReportCommandHandler
    : IRequestHandler<ExportStaffLeaderReportCommand, ExportStaffLeaderReportResult>
{
    private const string BrandBlue = "#004C91";

    private static readonly string[] AllSections =
    {
        "EXECUTIVE_SUMMARY",
        "LIFECYCLE_SUMMARY",
        "HOST_WORKLOAD",
        "PENDING_ACTIONS",
        "LOGISTICS_SUMMARY",
        "CLOSE_READINESS",
        "FEEDBACK_SUMMARY",
    };

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ExportStaffLeaderReportCommandHandler(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ExportStaffLeaderReportResult> Handle(ExportStaffLeaderReportCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(_currentUser.RoleCode, "STAFF", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(_currentUser.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền xuất báo cáo vận hành campus.");

        // Reuse the dashboard aggregation so the file reflects the exact filters currently applied.
        var overview = await _mediator.Send(new GetStaffLeaderReportOverviewQuery
        {
            Preset = request.Preset,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            VisitStatus = request.VisitStatus,
            RequestStatus = request.RequestStatus,
            HostUserId = request.HostUserId,
            DepartmentId = request.DepartmentId,
            LogisticsStatus = request.LogisticsStatus,
            FeedbackRating = request.FeedbackRating,
        }, cancellationToken);

        var sections = request.ReportSections is { Length: > 0 }
            ? request.ReportSections.Select(s => s.Trim().ToUpperInvariant()).Where(s => AllSections.Contains(s)).ToHashSet()
            : AllSections.ToHashSet();

        var format = request.ExportFormat?.Trim().ToUpperInvariant() switch
        {
            "PDF" => "PDF",
            "CSV" => "CSV",
            _ => "EXCEL",
        };

        var stampVn = DateTime.UtcNow.AddHours(7);
        var baseName = $"PEMS_StaffLeader_Campus_Report_{stampVn:yyyyMMdd_HHmm}";

        return format switch
        {
            "CSV" => new ExportStaffLeaderReportResult
            {
                Content = BuildCsv(overview, sections),
                ContentType = "text/csv; charset=utf-8",
                FileName = baseName + ".csv",
            },
            "PDF" => new ExportStaffLeaderReportResult
            {
                Content = BuildPdf(overview, sections),
                ContentType = "application/pdf",
                FileName = baseName + ".pdf",
            },
            _ => new ExportStaffLeaderReportResult
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

    private static string Dt(DateTime? d) =>
        d?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—";

    private static string Num(double? v) =>
        v?.ToString("0.#", CultureInfo.InvariantCulture) ?? "—";

    private static string AppliedFilters(StaffLeaderFilterSummary f)
    {
        var parts = new List<string>();
        if (f.VisitStatus != "ALL") parts.Add($"Trạng thái chuyến: {f.VisitStatus}");
        if (f.RequestStatus != "ALL") parts.Add($"Trạng thái đơn: {f.RequestStatus}");
        if (f.HostUserId != "ALL") parts.Add($"Host: {f.HostName ?? f.HostUserId}");
        if (f.DepartmentId != "ALL") parts.Add($"Phòng ban: {f.DepartmentName ?? f.DepartmentId}");
        if (f.LogisticsStatus != "ALL") parts.Add($"Logistics: {f.LogisticsStatus}");
        if (f.FeedbackRating != "ALL") parts.Add($"Rating: {f.FeedbackRating}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "Không có";
    }

    private static List<(string Label, string Value)> HeaderLines(StaffLeaderReportOverviewDto o) => new()
    {
        ("Hệ thống", "Partnership Engagement Management System"),
        ("Báo cáo", "Staff Leader Campus Operation Report"),
        ("Campus", o.FilterSummary.CampusName),
        ("Khoảng thời gian", $"{PresetLabel(o.FilterSummary.Preset)} ({o.FilterSummary.FromDate} – {o.FilterSummary.ToDate})"),
        ("Bộ lọc áp dụng", AppliedFilters(o.FilterSummary)),
        ("Người xuất", o.FilterSummary.GeneratedByName ?? "—"),
        ("Thời điểm xuất", o.GeneratedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (GMT+7)"),
    };

    private static List<(string Label, string Value)> KpiLines(StaffLeaderKpis k) => new()
    {
        ("Chờ duyệt", k.PendingSingleCampusApproval.ToString()),
        ("Chờ gán host", k.WaitingHostAssignment.ToString()),
        ("Đang chuẩn bị (đã gán + trước chuyến)", (k.AssignedVisits + k.BeforeVisit).ToString()),
        ("Đang diễn ra", k.DuringVisit.ToString()),
        ("Sau tiếp khách", k.AfterVisit.ToString()),
        ("Chưa đóng/quá hạn", k.OverdueOrNotClosed.ToString()),
        ("Đã đóng trong kỳ", k.ClosedVisits.ToString()),
        ("Feedback TB", Num(k.AverageFeedbackRating)),
        ("Tổng khách", k.TotalGuests.ToString()),
    };

    private static string LogisticsStateLabel(StaffLeaderCloseReadiness c) =>
        c.LogisticsOpenCount == 0 && c.MissingHandoverSignatureCount == 0
            ? "Đủ"
            : $"Còn mở {c.LogisticsOpenCount + c.MissingHandoverSignatureCount}";

    private static string MinutesStateLabel(StaffLeaderCloseReadiness c) =>
        c.HasMinutes ? (c.OpenActionItemCount == 0 ? "Đủ" : $"{c.OpenActionItemCount} việc mở") : "Thiếu";

    private static string NewsStateLabel(StaffLeaderCloseReadiness c) =>
        c.NewsNotRequired ? "Không cần" : (c.HasPublishedNews ? "Đã đăng" : "Thiếu");

    // ─────────────────────────────────── CSV ───────────────────────────────────

    private static byte[] BuildCsv(StaffLeaderReportOverviewDto o, HashSet<string> sections)
    {
        var sb = new StringBuilder();
        void Row(params object?[] cells) =>
            sb.AppendLine(string.Join(",", cells.Select(c =>
            {
                var s = Convert.ToString(c, CultureInfo.InvariantCulture) ?? "";
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            })));
        void NoData() => Row("No data available for this section");

        var f = o.FilterSummary;
        Row("Partnership Engagement Management System");
        Row("Staff Leader Campus Operation Report");
        Row("Campus", f.CampusName);
        Row("Period", $"{PresetLabel(f.Preset)} ({f.FromDate} - {f.ToDate})");
        Row("Generated by", f.GeneratedByName ?? "—");
        Row("Generated at", o.GeneratedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (GMT+7)");
        Row("Applied filters", AppliedFilters(f));
        Row("Export Format", "CSV");
        Row();

        if (sections.Contains("EXECUTIVE_SUMMARY"))
        {
            Row("1. EXECUTIVE SUMMARY");
            Row("Metric", "Value");
            foreach (var (label, value) in KpiLines(o.Kpis)) Row(label, value);
            Row();
            Row("Cần xử lý", "Số lượng", "Mức độ");
            foreach (var a in o.AttentionItems) Row(a.Label, a.Count, a.Severity);
            Row();
        }

        if (sections.Contains("LIFECYCLE_SUMMARY"))
        {
            Row("2. LIFECYCLE SUMMARY");
            Row("Status", "Count", "Percentage");
            if (o.CampusLifecyclePipeline.Count == 0) NoData();
            else foreach (var s in o.CampusLifecyclePipeline) Row(s.LabelVi, s.Count, $"{s.Percentage}%");
            Row();
            Row("Monthly Trend");
            Row("Month", "Total", "Closed", "Cancelled", "Active");
            if (o.MonthlyTrend.Count == 0) NoData();
            else foreach (var m in o.MonthlyTrend)
                Row(m.MonthLabel, m.TotalInstances, m.ClosedInstances, m.CancelledInstances, m.ActiveInstances);
            Row();
        }

        if (sections.Contains("HOST_WORKLOAD"))
        {
            Row("3. HOST WORKLOAD");
            Row("Host", "Assigned", "Upcoming 7 Days", "Before Visit", "During Visit", "After Visit", "Average Feedback");
            if (o.HostWorkload.Count == 0) NoData();
            else foreach (var h in o.HostWorkload)
                Row(h.HostName, h.AssignedCount, h.Upcoming7Days, h.BeforeVisitCount, h.DuringVisitCount,
                    h.AfterVisitCount, Num(h.AverageFeedbackRating));
            Row();
        }

        if (sections.Contains("PENDING_ACTIONS"))
        {
            Row("4. PENDING ACTIONS", $"Top {o.PendingActionRequests.Count}/{o.PendingActionTotal}");
            Row("Request Code", "Delegation Name", "Type", "Planned Date", "Guest Count", "Status", "Waiting Hours", "Action");
            if (o.PendingActionRequests.Count == 0) NoData();
            else foreach (var p in o.PendingActionRequests)
                Row(p.RequestCode, p.DelegationName, p.VisitType,
                    $"{Dt(p.PlannedStartAt)} - {Dt(p.PlannedEndAt)}", p.GuestCount, p.Status,
                    p.WaitingHours.ToString("0.#", CultureInfo.InvariantCulture), p.ActionLabel);
            Row();
        }

        if (sections.Contains("LOGISTICS_SUMMARY"))
        {
            Row("5. LOGISTICS SUMMARY");
            Row("Department", "Total", "Requested", "Accepted", "In Progress", "Done", "Rejected", "Overdue");
            if (o.LogisticsByDepartment.Count == 0) NoData();
            else foreach (var d in o.LogisticsByDepartment)
                Row(d.DepartmentName, d.TotalItems, d.Requested, d.Accepted, d.InProgress, d.Done, d.Rejected, d.OverdueCount);
            Row();
        }

        if (sections.Contains("CLOSE_READINESS"))
        {
            Row("6. CLOSE READINESS", $"Top {o.CloseReadiness.Count}/{o.CloseReadinessTotal}");
            Row("Delegation", "Host", "Planned End", "Logistics", "Minutes", "News", "Feedback", "Can Close", "Blockers");
            if (o.CloseReadiness.Count == 0) NoData();
            else foreach (var c in o.CloseReadiness)
                Row(c.DelegationName, c.HostName ?? "—", Dt(c.PlannedEndAt),
                    LogisticsStateLabel(c), MinutesStateLabel(c), NewsStateLabel(c),
                    c.FeedbackCount, c.CanClose ? "Có" : "Chưa", string.Join("; ", c.Blockers));
            Row();
        }

        if (sections.Contains("FEEDBACK_SUMMARY"))
        {
            var fb = o.FeedbackSummary;
            Row("7. FEEDBACK SUMMARY");
            Row("Average Rating", Num(fb.AverageRating));
            Row("Total Feedbacks", fb.TotalFeedbacks);
            Row("Low Feedbacks (<=2)", fb.LowFeedbackCount);
            Row();
            Row("Rating By Host");
            Row("Host", "Average Rating", "Feedback Count");
            if (fb.RatingByHost.Count == 0) NoData();
            else foreach (var h in fb.RatingByHost) Row(h.HostName, Num(h.AverageRating), h.FeedbackCount);
            Row();
            Row("Low Feedbacks");
            Row("Delegation", "Host", "Rating", "Comment", "Date");
            if (fb.LowFeedbacks.Count == 0) NoData();
            else foreach (var e in fb.LowFeedbacks)
                Row(e.DelegationName, e.HostName ?? "—", e.Rating, e.Comment ?? "", Dt(e.PlannedStartAt));
            Row();
            Row("Recent Good Feedbacks");
            Row("Delegation", "Host", "Rating", "Comment", "Date");
            if (fb.GoodFeedbacks.Count == 0) NoData();
            else foreach (var e in fb.GoodFeedbacks)
                Row(e.DelegationName, e.HostName ?? "—", e.Rating, e.Comment ?? "", Dt(e.PlannedStartAt));
        }

        // UTF-8 BOM so Excel opens Vietnamese text correctly.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // ────────────────────────────────── Excel ──────────────────────────────────

    private static byte[] BuildExcel(StaffLeaderReportOverviewDto o, HashSet<string> sections)
    {
        using var workbook = new XLWorkbook();

        void StyleHeader(IXLWorksheet ws, int row, int colCount)
        {
            var range = ws.Range(row, 1, row, colCount);
            range.Style.Fill.BackgroundColor = XLColor.FromHtml(BrandBlue);
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Font.Bold = true;
        }

        int WriteTable(IXLWorksheet ws, int startRow, string[] header, IEnumerable<object?[]> rows, string? title = null)
        {
            var r = startRow;
            if (title != null)
            {
                ws.Cell(r, 1).Value = title;
                ws.Cell(r, 1).Style.Font.Bold = true;
                r++;
            }
            for (var c = 0; c < header.Length; c++) ws.Cell(r, c + 1).Value = header[c];
            StyleHeader(ws, r, header.Length);
            r++;
            var any = false;
            foreach (var row in rows)
            {
                any = true;
                for (var c = 0; c < row.Length; c++)
                {
                    var cell = ws.Cell(r, c + 1);
                    switch (row[c])
                    {
                        case int i: cell.Value = i; break;
                        case double d: cell.Value = d; break;
                        default: cell.Value = Convert.ToString(row[c], CultureInfo.InvariantCulture) ?? ""; break;
                    }
                }
                r++;
            }
            if (!any)
            {
                ws.Cell(r, 1).Value = "No data available for this section";
                ws.Cell(r, 1).Style.Font.FontColor = XLColor.Gray;
                r++;
            }
            return r + 1; // one blank row after each table
        }

        // Sheet 1: overview (always present so the file always has context).
        var wsInfo = workbook.Worksheets.Add("Tổng quan");
        var ir = 1;
        foreach (var (label, value) in HeaderLines(o))
        {
            wsInfo.Cell(ir, 1).Value = label;
            wsInfo.Cell(ir, 1).Style.Font.Bold = true;
            wsInfo.Cell(ir, 2).Value = value;
            ir++;
        }

        if (sections.Contains("EXECUTIVE_SUMMARY"))
        {
            ir++;
            ir = WriteTable(wsInfo, ir, new[] { "KPI", "Giá trị" },
                KpiLines(o.Kpis).Select(k => new object?[] { k.Label, k.Value }));
            ir = WriteTable(wsInfo, ir, new[] { "Cần xử lý", "Số lượng", "Mức độ" },
                o.AttentionItems.Select(a => new object?[] { a.Label, a.Count, a.Severity }));
        }
        wsInfo.Columns().AdjustToContents(1, 60);

        if (sections.Contains("LIFECYCLE_SUMMARY"))
        {
            var ws = workbook.Worksheets.Add("Vòng đời & Xu hướng");
            var r = WriteTable(ws, 1, new[] { "Trạng thái", "Số lượng", "Tỷ lệ (%)" },
                o.CampusLifecyclePipeline.Select(s => new object?[] { s.LabelVi, s.Count, s.Percentage }));
            WriteTable(ws, r, new[] { "Tháng", "Tổng chuyến", "Đã đóng", "Bị hủy", "Đang xử lý" },
                o.MonthlyTrend.Select(m => new object?[] { m.MonthLabel, m.TotalInstances, m.ClosedInstances, m.CancelledInstances, m.ActiveInstances }),
                "Xu hướng theo tháng");
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("HOST_WORKLOAD"))
        {
            var ws = workbook.Worksheets.Add("Khối lượng host");
            WriteTable(ws, 1,
                new[] { "Host", "Đang phụ trách", "Sắp tới 7 ngày", "Trước chuyến", "Đang diễn ra", "Sau chuyến", "Feedback TB" },
                o.HostWorkload.Select(h => new object?[]
                {
                    h.HostName, h.AssignedCount, h.Upcoming7Days, h.BeforeVisitCount,
                    h.DuringVisitCount, h.AfterVisitCount, Num(h.AverageFeedbackRating),
                }));
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("PENDING_ACTIONS"))
        {
            var ws = workbook.Worksheets.Add("Cần xử lý");
            WriteTable(ws, 1,
                new[] { "Mã đơn", "Tên đoàn", "Loại chuyến", "Ngày thăm", "Số khách", "Trạng thái", "Giờ chờ", "Hành động" },
                o.PendingActionRequests.Select(p => new object?[]
                {
                    p.RequestCode, p.DelegationName, p.VisitType,
                    $"{Dt(p.PlannedStartAt)} – {Dt(p.PlannedEndAt)}", p.GuestCount, p.Status,
                    p.WaitingHours, p.ActionLabel,
                }),
                $"Đơn đang chờ Staff Leader xử lý (top {o.PendingActionRequests.Count}/{o.PendingActionTotal})");
            ws.Columns().AdjustToContents(1, 60);
        }

        if (sections.Contains("LOGISTICS_SUMMARY"))
        {
            var ws = workbook.Worksheets.Add("Logistics");
            WriteTable(ws, 1,
                new[] { "Phòng ban", "Tổng", "Chờ phản hồi", "Đã nhận", "Đang xử lý", "Hoàn thành", "Từ chối", "Quá hạn" },
                o.LogisticsByDepartment.Select(d => new object?[]
                {
                    d.DepartmentName, d.TotalItems, d.Requested, d.Accepted, d.InProgress, d.Done, d.Rejected, d.OverdueCount,
                }));
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("CLOSE_READINESS"))
        {
            var ws = workbook.Worksheets.Add("Đóng hồ sơ");
            WriteTable(ws, 1,
                new[] { "Đoàn", "Host", "Kết thúc", "Logistics", "Biên bản", "News", "Feedback", "Có thể đóng", "Vướng mắc" },
                o.CloseReadiness.Select(c => new object?[]
                {
                    c.DelegationName, c.HostName ?? "—", Dt(c.PlannedEndAt),
                    LogisticsStateLabel(c), MinutesStateLabel(c), NewsStateLabel(c),
                    c.FeedbackCount, c.CanClose ? "Có" : "Chưa", string.Join("; ", c.Blockers),
                }),
                $"Hồ sơ sau tiếp khách cần hoàn tất (top {o.CloseReadiness.Count}/{o.CloseReadinessTotal})");
            ws.Columns().AdjustToContents(1, 60);
        }

        if (sections.Contains("FEEDBACK_SUMMARY"))
        {
            var fb = o.FeedbackSummary;
            var ws = workbook.Worksheets.Add("Feedback");
            var r = WriteTable(ws, 1, new[] { "Chỉ số", "Giá trị" }, new List<object?[]>
            {
                new object?[] { "Điểm trung bình", Num(fb.AverageRating) },
                new object?[] { "Tổng feedback", fb.TotalFeedbacks },
                new object?[] { "Feedback thấp (≤2)", fb.LowFeedbackCount },
            });
            r = WriteTable(ws, r, new[] { "Host", "Điểm TB", "Số feedback" },
                fb.RatingByHost.Select(h => new object?[] { h.HostName, h.AverageRating, h.FeedbackCount }),
                "Đánh giá theo host");
            r = WriteTable(ws, r, new[] { "Đoàn", "Host", "Rating", "Nội dung", "Ngày thăm" },
                fb.LowFeedbacks.Select(e => new object?[] { e.DelegationName, e.HostName ?? "—", e.Rating, e.Comment ?? "", Dt(e.PlannedStartAt) }),
                "Feedback thấp cần chú ý");
            WriteTable(ws, r, new[] { "Đoàn", "Host", "Rating", "Nội dung", "Ngày thăm" },
                fb.GoodFeedbacks.Select(e => new object?[] { e.DelegationName, e.HostName ?? "—", e.Rating, e.Comment ?? "", Dt(e.PlannedStartAt) }),
                "Feedback tốt gần đây");
            ws.Columns().AdjustToContents(1, 60);
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─────────────────────────────────── PDF ───────────────────────────────────

    private static byte[] BuildPdf(StaffLeaderReportOverviewDto o, HashSet<string> sections)
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
                    header.Item().Text("STAFF LEADER CAMPUS OPERATION REPORT")
                        .FontSize(16).Bold().FontColor(headerBg);
                    header.Item().PaddingTop(4).Text(t =>
                    {
                        t.Span($"Campus: {o.FilterSummary.CampusName}   ");
                        t.Span($"Kỳ: {PresetLabel(o.FilterSummary.Preset)} ({o.FilterSummary.FromDate} – {o.FilterSummary.ToDate})");
                    });
                    header.Item().Text($"Bộ lọc: {AppliedFilters(o.FilterSummary)}")
                        .FontSize(8).FontColor(muted);
                    header.Item().Text($"Người xuất: {o.FilterSummary.GeneratedByName ?? "—"} · {o.GeneratedAt.AddHours(7):dd/MM/yyyy HH:mm} (GMT+7)")
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

                    void DataTable(string[] headerCells, IReadOnlyList<string[]> rows, float[]? weights = null)
                    {
                        if (rows.Count == 0)
                        {
                            col.Item().Padding(3).Text("No data available for this section").FontSize(8).FontColor(muted);
                            return;
                        }
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
                            new[] { "Cần xử lý", "Số lượng", "Mức độ" },
                            o.AttentionItems.Select(a => new[] { a.Label, a.Count.ToString(), a.Severity }).ToList(),
                            new[] { 4f, 1f, 1.2f });
                    }

                    if (sections.Contains("LIFECYCLE_SUMMARY"))
                    {
                        SectionTitle("2. Lifecycle & Trend");
                        DataTable(
                            new[] { "Trạng thái", "Số lượng", "Tỷ lệ" },
                            o.CampusLifecyclePipeline.Select(s => new[] { s.LabelVi, s.Count.ToString(), $"{s.Percentage}%" }).ToList(),
                            new[] { 3f, 1f, 1f });
                        col.Item().PaddingTop(6).Text("Xu hướng theo tháng").FontSize(9).Bold();
                        col.Item().PaddingTop(2);
                        DataTable(
                            new[] { "Tháng", "Tổng chuyến", "Đã đóng", "Bị hủy", "Đang xử lý" },
                            o.MonthlyTrend.Select(m => new[]
                            {
                                m.MonthLabel, m.TotalInstances.ToString(), m.ClosedInstances.ToString(),
                                m.CancelledInstances.ToString(), m.ActiveInstances.ToString(),
                            }).ToList());
                    }

                    if (sections.Contains("HOST_WORKLOAD"))
                    {
                        SectionTitle("3. Host Workload");
                        DataTable(
                            new[] { "Host", "Phụ trách", "7 ngày tới", "Trước chuyến", "Đang tiếp", "Sau chuyến", "FB TB" },
                            o.HostWorkload.Select(h => new[]
                            {
                                h.HostName, h.AssignedCount.ToString(), h.Upcoming7Days.ToString(),
                                h.BeforeVisitCount.ToString(), h.DuringVisitCount.ToString(),
                                h.AfterVisitCount.ToString(), Num(h.AverageFeedbackRating),
                            }).ToList(),
                            new[] { 2.4f, 1f, 1f, 1.1f, 1f, 1.1f, 0.8f });
                    }

                    if (sections.Contains("PENDING_ACTIONS"))
                    {
                        SectionTitle($"4. Pending Actions (top {o.PendingActionRequests.Count}/{o.PendingActionTotal})");
                        DataTable(
                            new[] { "Mã đơn", "Đoàn", "Ngày thăm", "Khách", "Trạng thái", "Giờ chờ", "Hành động" },
                            o.PendingActionRequests.Select(p => new[]
                            {
                                p.RequestCode, p.DelegationName,
                                $"{Dt(p.PlannedStartAt)} – {Dt(p.PlannedEndAt)}", p.GuestCount.ToString(),
                                p.Status, p.WaitingHours.ToString("0", CultureInfo.InvariantCulture), p.ActionLabel,
                            }).ToList(),
                            new[] { 1.4f, 2.4f, 1.9f, 0.7f, 1.8f, 0.8f, 1.2f });
                    }

                    if (sections.Contains("LOGISTICS_SUMMARY"))
                    {
                        SectionTitle("5. Logistics Summary");
                        DataTable(
                            new[] { "Phòng ban", "Tổng", "Chờ phản hồi", "Đã nhận", "Đang xử lý", "Hoàn thành", "Từ chối", "Quá hạn" },
                            o.LogisticsByDepartment.Select(d => new[]
                            {
                                d.DepartmentName, d.TotalItems.ToString(), d.Requested.ToString(), d.Accepted.ToString(),
                                d.InProgress.ToString(), d.Done.ToString(), d.Rejected.ToString(), d.OverdueCount.ToString(),
                            }).ToList(),
                            new[] { 2.6f, 0.7f, 1.1f, 0.9f, 1f, 1f, 0.9f, 0.9f });
                    }

                    if (sections.Contains("CLOSE_READINESS"))
                    {
                        SectionTitle($"6. Close Readiness (top {o.CloseReadiness.Count}/{o.CloseReadinessTotal})");
                        DataTable(
                            new[] { "Đoàn", "Host", "Kết thúc", "Logistics", "Biên bản", "News", "FB", "Đóng được" },
                            o.CloseReadiness.Select(c => new[]
                            {
                                c.DelegationName, c.HostName ?? "—", Dt(c.PlannedEndAt),
                                LogisticsStateLabel(c), MinutesStateLabel(c), NewsStateLabel(c),
                                c.FeedbackCount.ToString(), c.CanClose ? "Có" : "Chưa",
                            }).ToList(),
                            new[] { 2.4f, 1.6f, 1.2f, 1.1f, 1.1f, 1.1f, 0.6f, 1f });
                    }

                    if (sections.Contains("FEEDBACK_SUMMARY"))
                    {
                        var fb = o.FeedbackSummary;
                        SectionTitle("7. Feedback Summary");
                        KeyValueTable(new List<(string, string)>
                        {
                            ("Điểm trung bình", Num(fb.AverageRating)),
                            ("Tổng feedback", fb.TotalFeedbacks.ToString()),
                            ("Feedback thấp (≤2)", fb.LowFeedbackCount.ToString()),
                        });
                        if (fb.RatingByHost.Count > 0)
                        {
                            col.Item().PaddingTop(6).Text("Đánh giá theo host").FontSize(9).Bold();
                            col.Item().PaddingTop(2);
                            DataTable(
                                new[] { "Host", "Điểm TB", "Số feedback" },
                                fb.RatingByHost.Select(h => new[] { h.HostName, Num(h.AverageRating), h.FeedbackCount.ToString() }).ToList(),
                                new[] { 3f, 1f, 1f });
                        }
                        col.Item().PaddingTop(6).Text("Feedback thấp cần chú ý").FontSize(9).Bold();
                        col.Item().PaddingTop(2);
                        DataTable(
                            new[] { "Đoàn", "Host", "Rating", "Nội dung", "Ngày thăm" },
                            fb.LowFeedbacks.Select(e => new[]
                            {
                                e.DelegationName, e.HostName ?? "—", e.Rating.ToString(), e.Comment ?? "", Dt(e.PlannedStartAt),
                            }).ToList(),
                            new[] { 2.2f, 1.6f, 0.7f, 3f, 1.1f });
                        col.Item().PaddingTop(6).Text("Feedback tốt gần đây").FontSize(9).Bold();
                        col.Item().PaddingTop(2);
                        DataTable(
                            new[] { "Đoàn", "Host", "Rating", "Nội dung", "Ngày thăm" },
                            fb.GoodFeedbacks.Select(e => new[]
                            {
                                e.DelegationName, e.HostName ?? "—", e.Rating.ToString(), e.Comment ?? "", Dt(e.PlannedStartAt),
                            }).ToList(),
                            new[] { 2.2f, 1.6f, 0.7f, 3f, 1.1f });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(muted));
                    t.Span("PEMS · Staff Leader Campus Report · Trang ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}

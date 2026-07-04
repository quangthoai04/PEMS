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
using PEMS.Application.Reports.Queries.GetDeptLeaderReportOverview;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Reports.Commands.ExportDeptLeaderReport;

/// <summary>
/// Builds the Department Leader report file (CSV / Excel via ClosedXML / PDF via QuestPDF)
/// from the same aggregation as the dashboard (GetDeptLeaderReportOverviewQuery), so the
/// export always matches the filters on screen.
/// </summary>
public sealed class ExportDeptLeaderReportCommandHandler
    : IRequestHandler<ExportDeptLeaderReportCommand, ExportDeptLeaderReportResult>
{
    private const string BrandBlue = "#004C91";

    private static readonly string[] AllSections =
    {
        "EXECUTIVE_SUMMARY",
        "TASK_PIPELINE",
        "STAFF_PERFORMANCE",
        "HANDOVER_SUMMARY",
        "INCIDENT_SUMMARY",
        "FEEDBACK_SUMMARY",
    };

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ExportDeptLeaderReportCommandHandler(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ExportDeptLeaderReportResult> Handle(ExportDeptLeaderReportCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(_currentUser.RoleCode, "DEPARTMENT", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(_currentUser.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền xuất báo cáo phòng ban.");

        // Reuse the dashboard aggregation so the file reflects the exact filters currently applied.
        var overview = await _mediator.Send(new GetDeptLeaderReportOverviewQuery
        {
            Preset = request.Preset,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            LogisticsStatus = request.LogisticsStatus,
            ItemType = request.ItemType,
            Priority = request.Priority,
            AssignedUserId = request.AssignedUserId,
            DueStatus = request.DueStatus,
            HandoverStatus = request.HandoverStatus,
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
        var baseName = $"PEMS_DepartmentLeader_Report_{stampVn:yyyyMMdd_HHmm}";

        return format switch
        {
            "CSV" => new ExportDeptLeaderReportResult
            {
                Content = BuildCsv(overview, sections),
                ContentType = "text/csv; charset=utf-8",
                FileName = baseName + ".csv",
            },
            "PDF" => new ExportDeptLeaderReportResult
            {
                Content = BuildPdf(overview, sections),
                ContentType = "application/pdf",
                FileName = baseName + ".pdf",
            },
            _ => new ExportDeptLeaderReportResult
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
        "THIS_QUARTER" => "Quý này",
        "THIS_YEAR" => "Năm nay",
        "CUSTOM" => "Tùy chỉnh",
        _ => "Tháng này",
    };

    private static string Dt(DateTime? d) =>
        d?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—";

    private static string Num(double? v) =>
        v?.ToString("0.#", CultureInfo.InvariantCulture) ?? "—";

    private static string AppliedFilters(DeptLeaderFilterSummary f)
    {
        var parts = new List<string>();
        if (f.LogisticsStatus != "ALL") parts.Add($"Trạng thái: {DeptLeaderReportLabels.StatusLabelVi(f.LogisticsStatus)}");
        if (f.ItemType != "ALL") parts.Add($"Mảng việc: {DeptLeaderReportLabels.ItemTypeLabelVi(f.ItemType)}");
        if (f.Priority != "ALL") parts.Add($"Ưu tiên: {DeptLeaderReportLabels.PriorityLabelVi(f.Priority)}");
        if (f.AssignedUserId != "ALL") parts.Add($"Nhân sự: {f.AssignedUserName ?? f.AssignedUserId}");
        if (f.DueStatus != "ALL") parts.Add($"Deadline: {(f.DueStatus == "OVERDUE" ? "Quá hạn" : "Sắp đến hạn")}");
        if (f.HandoverStatus != "ALL") parts.Add($"Bàn giao: {f.HandoverStatus}");
        if (f.FeedbackRating != "ALL") parts.Add($"Rating: {f.FeedbackRating}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "Không có";
    }

    private static List<(string Label, string Value)> HeaderLines(DeptLeaderReportOverviewDto o) => new()
    {
        ("Hệ thống", "Partnership Engagement Management System"),
        ("Báo cáo", "Department Leader Operation Report"),
        ("Phòng ban", o.FilterSummary.DepartmentName),
        ("Campus", o.FilterSummary.CampusName),
        ("Khoảng thời gian", $"{PresetLabel(o.FilterSummary.Preset)} ({o.FilterSummary.FromDate} – {o.FilterSummary.ToDate})"),
        ("Bộ lọc áp dụng", AppliedFilters(o.FilterSummary)),
        ("Người xuất", o.FilterSummary.GeneratedByName ?? "—"),
        ("Thời điểm xuất", o.GeneratedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (GMT+7)"),
    };

    private static List<(string Label, string Value)> KpiLines(DeptLeaderKpis k) => new()
    {
        ("Yêu cầu mới", k.NewRequests.ToString()),
        ("Chưa phân công", k.WaitingAssignment.ToString()),
        ("Chờ nhân sự phản hồi", k.WaitingStaffResponse.ToString()),
        ("Đang xử lý", k.InProgress.ToString()),
        ("Hoàn thành trong kỳ", k.Completed.ToString()),
        ("Nhân sự từ chối trong kỳ", k.Declined.ToString()),
        ("Quá hạn", k.Overdue.ToString()),
        ("Thiếu chữ ký bàn giao", k.MissingHandoverSignature.ToString()),
        ("Thời gian phản hồi TB (giờ)", Num(k.AverageResponseHours)),
        ("Feedback TB", Num(k.AverageFeedbackRating)),
    };

    // ─────────────────────────────────── CSV ───────────────────────────────────

    private static byte[] BuildCsv(DeptLeaderReportOverviewDto o, HashSet<string> sections)
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
        Row("Department Leader Operation Report");
        Row("Department", f.DepartmentName);
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

        if (sections.Contains("TASK_PIPELINE"))
        {
            Row("2. TASK PIPELINE");
            Row("Status", "Count", "Percentage");
            if (o.TaskStatusPipeline.Count == 0) NoData();
            else foreach (var s in o.TaskStatusPipeline) Row(s.LabelVi, s.Count, $"{s.Percentage}%");
            Row();
            Row("Work Type Distribution");
            Row("Item Type", "Count", "Total Quantity", "Percentage");
            if (o.WorkTypeDistribution.Count == 0) NoData();
            else foreach (var w in o.WorkTypeDistribution) Row(w.LabelVi, w.Count, w.QuantityTotal, $"{w.Percentage}%");
            Row();
            Row("Monthly Trend");
            Row("Month", "Total", "Completed", "Overdue");
            if (o.MonthlyTrend.Count == 0) NoData();
            else foreach (var m in o.MonthlyTrend) Row(m.MonthLabel, m.TotalTasks, m.CompletedTasks, m.OverdueTasks);
            Row();
            Row("Pending Tasks", $"Top {o.PendingTasks.Count}/{o.PendingTasksTotal}");
            Row("Item", "Visit", "Type", "Quantity", "Priority", "Status", "Deadline", "Assignee", "Waiting Hours", "Action");
            if (o.PendingTasks.Count == 0) NoData();
            else foreach (var t in o.PendingTasks)
                Row(t.ItemName, $"{t.RequestCode} · {t.DelegationName}", DeptLeaderReportLabels.ItemTypeLabelVi(t.ItemType),
                    t.Quantity, DeptLeaderReportLabels.PriorityLabelVi(t.Priority), DeptLeaderReportLabels.StatusLabelVi(t.Status),
                    Dt(t.DueAt), t.AssignedToName ?? "—", t.WaitingHours.ToString("0.#", CultureInfo.InvariantCulture), t.ActionLabel);
            Row();
            Row("Change Proposals");
            Row("Item", "Proposed By", "Proposed Quantity", "Proposed Time", "Note", "Status", "Created At");
            if (o.ProposalChanges.Count == 0) NoData();
            else foreach (var p in o.ProposalChanges)
                Row(p.ItemName, p.ProposedByName, p.ProposedQuantity?.ToString() ?? "—",
                    $"{Dt(p.ProposedUsageStartAt)} - {Dt(p.ProposedUsageEndAt)}", p.ProposalNote ?? "", p.ProposalStatus, Dt(p.CreatedAt));
            Row();
        }

        if (sections.Contains("STAFF_PERFORMANCE"))
        {
            Row("3. STAFF PERFORMANCE");
            Row("Staff", "Assigned", "Pending Response", "Accepted", "In Progress", "Completed", "Declined", "Overdue", "Completion Rate", "Avg Response Hours");
            if (o.StaffPerformance.Count == 0) NoData();
            else foreach (var s in o.StaffPerformance)
                Row(s.FullName, s.AssignedCount, s.PendingResponseCount, s.AcceptedCount, s.InProgressCount,
                    s.CompletedCount, s.DeclinedCount, s.OverdueCount, $"{s.CompletionRate}%", Num(s.AverageResponseHours));
            Row();
        }

        if (sections.Contains("HANDOVER_SUMMARY"))
        {
            Row("4. HANDOVER SUMMARY", $"Top {o.HandoverSummary.Count}/{o.HandoverTotal}");
            Row("Item", "Visit", "Type", "Borrower Signed", "Provider Signed", "Condition", "Note", "Status");
            if (o.HandoverSummary.Count == 0) NoData();
            else foreach (var h in o.HandoverSummary)
                Row(h.ItemName, $"{h.VisitCode} · {h.DelegationName}", h.HandoverType == "BORROW" ? "Ký mượn" : "Ký trả",
                    h.BorrowerSigned ? "Có" : "Chưa", h.ProviderSigned ? "Có" : "Chưa",
                    h.ItemCondition ?? "—", h.ConditionNote ?? "", h.StatusLabel);
            Row();
        }

        if (sections.Contains("INCIDENT_SUMMARY"))
        {
            Row("5. INCIDENTS AFTER HANDOVER");
            Row("Work Type", "Total Quantity", "Damaged", "Missing", "Need Action", "Latest Note");
            if (o.IncidentSummary.Count == 0) NoData();
            else foreach (var i in o.IncidentSummary)
                Row(i.ItemTypeLabelVi, i.TotalQuantity, i.DamagedCount, i.MissingCount, i.NeedActionCount, i.LatestNote ?? "");
            Row();
        }

        if (sections.Contains("FEEDBACK_SUMMARY"))
        {
            var fb = o.FeedbackSummary;
            Row("6. FEEDBACK SUMMARY");
            Row("Average Rating", Num(fb.AverageRating));
            Row("Total Feedbacks", fb.TotalFeedbacks);
            Row("Low Feedbacks (<=2)", fb.LowFeedbackCount);
            Row();
            Row("Rating By Work Type");
            Row("Work Type", "Average Rating", "Feedback Count");
            if (fb.FeedbackByItemType.Count == 0) NoData();
            else foreach (var t in fb.FeedbackByItemType) Row(t.LabelVi, Num(t.AverageRating), t.FeedbackCount);
            Row();
            Row("Low Rated Feedbacks");
            Row("Delegation", "Target", "Rating", "Comment", "Submitted At");
            if (fb.LowRatedItems.Count == 0) NoData();
            else foreach (var e in fb.LowRatedItems)
                Row(e.DelegationName, e.ItemName ?? "—", e.Rating, e.Comment ?? "", Dt(e.SubmittedAt));
            Row();
            Row("Recent Feedbacks");
            Row("Delegation", "Target", "Rating", "Comment", "Submitted At");
            if (fb.RecentFeedbacks.Count == 0) NoData();
            else foreach (var e in fb.RecentFeedbacks)
                Row(e.DelegationName, e.ItemName ?? "—", e.Rating, e.Comment ?? "", Dt(e.SubmittedAt));
        }

        // UTF-8 BOM so Excel opens Vietnamese text correctly.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // ────────────────────────────────── Excel ──────────────────────────────────

    private static byte[] BuildExcel(DeptLeaderReportOverviewDto o, HashSet<string> sections)
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

        if (sections.Contains("TASK_PIPELINE"))
        {
            var ws = workbook.Worksheets.Add("Công việc");
            var r = WriteTable(ws, 1, new[] { "Trạng thái", "Số lượng", "Tỷ lệ (%)" },
                o.TaskStatusPipeline.Select(s => new object?[] { s.LabelVi, s.Count, s.Percentage }));
            r = WriteTable(ws, r, new[] { "Mảng việc", "Số nhiệm vụ", "Tổng số lượng", "Tỷ lệ (%)" },
                o.WorkTypeDistribution.Select(w => new object?[] { w.LabelVi, w.Count, w.QuantityTotal, w.Percentage }),
                "Phân bổ mảng việc");
            r = WriteTable(ws, r, new[] { "Tháng", "Tổng nhiệm vụ", "Hoàn thành", "Quá hạn" },
                o.MonthlyTrend.Select(m => new object?[] { m.MonthLabel, m.TotalTasks, m.CompletedTasks, m.OverdueTasks }),
                "Xu hướng theo tháng");
            r = WriteTable(ws, r,
                new[] { "Nhiệm vụ", "Đoàn/Visit", "Mảng việc", "Số lượng", "Ưu tiên", "Trạng thái", "Deadline", "Người xử lý", "Giờ chờ", "Hành động" },
                o.PendingTasks.Select(t => new object?[]
                {
                    t.ItemName, $"{t.RequestCode} · {t.DelegationName}", DeptLeaderReportLabels.ItemTypeLabelVi(t.ItemType),
                    t.Quantity, DeptLeaderReportLabels.PriorityLabelVi(t.Priority), DeptLeaderReportLabels.StatusLabelVi(t.Status),
                    Dt(t.DueAt), t.AssignedToName ?? "—", t.WaitingHours, t.ActionLabel,
                }),
                $"Nhiệm vụ cần xử lý (top {o.PendingTasks.Count}/{o.PendingTasksTotal})");
            WriteTable(ws, r,
                new[] { "Nhiệm vụ", "Người đề xuất", "SL đề xuất", "Thời gian đề xuất", "Ghi chú", "Trạng thái", "Ngày tạo" },
                o.ProposalChanges.Select(p => new object?[]
                {
                    p.ItemName, p.ProposedByName, p.ProposedQuantity?.ToString() ?? "—",
                    $"{Dt(p.ProposedUsageStartAt)} – {Dt(p.ProposedUsageEndAt)}", p.ProposalNote ?? "", p.ProposalStatus, Dt(p.CreatedAt),
                }),
                "Đề xuất thay đổi");
            ws.Columns().AdjustToContents(1, 60);
        }

        if (sections.Contains("STAFF_PERFORMANCE"))
        {
            var ws = workbook.Worksheets.Add("Nhân sự");
            WriteTable(ws, 1,
                new[] { "Nhân sự", "Được giao", "Chờ phản hồi", "Đã nhận", "Đang xử lý", "Hoàn thành", "Từ chối", "Quá hạn", "Tỷ lệ HT (%)", "Phản hồi TB (giờ)" },
                o.StaffPerformance.Select(s => new object?[]
                {
                    s.FullName, s.AssignedCount, s.PendingResponseCount, s.AcceptedCount, s.InProgressCount,
                    s.CompletedCount, s.DeclinedCount, s.OverdueCount, s.CompletionRate, Num(s.AverageResponseHours),
                }));
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("HANDOVER_SUMMARY"))
        {
            var ws = workbook.Worksheets.Add("Bàn giao");
            WriteTable(ws, 1,
                new[] { "Item", "Visit", "Loại bàn giao", "Bên mượn ký", "Bên giao ký", "Tình trạng", "Ghi chú", "Trạng thái" },
                o.HandoverSummary.Select(h => new object?[]
                {
                    h.ItemName, $"{h.VisitCode} · {h.DelegationName}", h.HandoverType == "BORROW" ? "Ký mượn" : "Ký trả",
                    h.BorrowerSigned ? "Có" : "Chưa", h.ProviderSigned ? "Có" : "Chưa",
                    h.ItemCondition ?? "—", h.ConditionNote ?? "", h.StatusLabel,
                }),
                $"Bàn giao/ký nhận (top {o.HandoverSummary.Count}/{o.HandoverTotal})");
            ws.Columns().AdjustToContents(1, 60);
        }

        if (sections.Contains("INCIDENT_SUMMARY"))
        {
            var ws = workbook.Worksheets.Add("Phát sinh");
            WriteTable(ws, 1,
                new[] { "Mảng việc", "Tổng số lượng", "Hư hỏng", "Thiếu/mất", "Cần xử lý", "Ghi chú mới nhất" },
                o.IncidentSummary.Select(i => new object?[]
                {
                    i.ItemTypeLabelVi, i.TotalQuantity, i.DamagedCount, i.MissingCount, i.NeedActionCount, i.LatestNote ?? "",
                }),
                "Phát sinh sau bàn giao");
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
            r = WriteTable(ws, r, new[] { "Mảng việc", "Điểm TB", "Số feedback" },
                fb.FeedbackByItemType.Select(t => new object?[] { t.LabelVi, t.AverageRating, t.FeedbackCount }),
                "Đánh giá theo mảng việc");
            r = WriteTable(ws, r, new[] { "Đoàn", "Đối tượng", "Rating", "Nội dung", "Ngày gửi" },
                fb.LowRatedItems.Select(e => new object?[] { e.DelegationName, e.ItemName ?? "—", e.Rating, e.Comment ?? "", Dt(e.SubmittedAt) }),
                "Feedback thấp cần chú ý");
            WriteTable(ws, r, new[] { "Đoàn", "Đối tượng", "Rating", "Nội dung", "Ngày gửi" },
                fb.RecentFeedbacks.Select(e => new object?[] { e.DelegationName, e.ItemName ?? "—", e.Rating, e.Comment ?? "", Dt(e.SubmittedAt) }),
                "Feedback gần đây");
            ws.Columns().AdjustToContents(1, 60);
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─────────────────────────────────── PDF ───────────────────────────────────

    private static byte[] BuildPdf(DeptLeaderReportOverviewDto o, HashSet<string> sections)
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
                    header.Item().Text("DEPARTMENT LEADER OPERATION REPORT")
                        .FontSize(16).Bold().FontColor(headerBg);
                    header.Item().PaddingTop(4).Text(t =>
                    {
                        t.Span($"Phòng ban: {o.FilterSummary.DepartmentName}   ");
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

                    void SubTitle(string text)
                    {
                        col.Item().PaddingTop(6).Text(text).FontSize(9).Bold();
                        col.Item().PaddingTop(2);
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

                    if (sections.Contains("TASK_PIPELINE"))
                    {
                        SectionTitle("2. Công việc phòng ban");
                        DataTable(
                            new[] { "Trạng thái", "Số lượng", "Tỷ lệ" },
                            o.TaskStatusPipeline.Select(s => new[] { s.LabelVi, s.Count.ToString(), $"{s.Percentage}%" }).ToList(),
                            new[] { 3f, 1f, 1f });
                        SubTitle("Phân bổ mảng việc");
                        DataTable(
                            new[] { "Mảng việc", "Số nhiệm vụ", "Tổng SL", "Tỷ lệ" },
                            o.WorkTypeDistribution.Select(w => new[] { w.LabelVi, w.Count.ToString(), w.QuantityTotal.ToString(), $"{w.Percentage}%" }).ToList(),
                            new[] { 3f, 1.2f, 1f, 1f });
                        SubTitle("Xu hướng theo tháng");
                        DataTable(
                            new[] { "Tháng", "Tổng nhiệm vụ", "Hoàn thành", "Quá hạn" },
                            o.MonthlyTrend.Select(m => new[] { m.MonthLabel, m.TotalTasks.ToString(), m.CompletedTasks.ToString(), m.OverdueTasks.ToString() }).ToList());
                        SubTitle($"Nhiệm vụ cần xử lý (top {o.PendingTasks.Count}/{o.PendingTasksTotal})");
                        DataTable(
                            new[] { "Nhiệm vụ", "Đoàn", "Mảng việc", "SL", "Ưu tiên", "Trạng thái", "Deadline", "Người xử lý" },
                            o.PendingTasks.Select(t => new[]
                            {
                                t.ItemName, t.DelegationName, DeptLeaderReportLabels.ItemTypeLabelVi(t.ItemType), t.Quantity.ToString(),
                                DeptLeaderReportLabels.PriorityLabelVi(t.Priority), DeptLeaderReportLabels.StatusLabelVi(t.Status),
                                Dt(t.DueAt), t.AssignedToName ?? "—",
                            }).ToList(),
                            new[] { 2.2f, 2f, 1.5f, 0.6f, 1f, 1.5f, 1.1f, 1.5f });
                        SubTitle("Đề xuất thay đổi");
                        DataTable(
                            new[] { "Nhiệm vụ", "Người đề xuất", "SL đề xuất", "Ghi chú", "Ngày tạo" },
                            o.ProposalChanges.Select(p => new[]
                            {
                                p.ItemName, p.ProposedByName, p.ProposedQuantity?.ToString() ?? "—", p.ProposalNote ?? "", Dt(p.CreatedAt),
                            }).ToList(),
                            new[] { 2.4f, 1.8f, 1f, 2.6f, 1.1f });
                    }

                    if (sections.Contains("STAFF_PERFORMANCE"))
                    {
                        SectionTitle("3. Hiệu suất nhân sự phòng ban");
                        DataTable(
                            new[] { "Nhân sự", "Giao", "Chờ PH", "Nhận", "Đang XL", "HT", "Từ chối", "Quá hạn", "Tỷ lệ HT", "PH TB (h)" },
                            o.StaffPerformance.Select(s => new[]
                            {
                                s.FullName, s.AssignedCount.ToString(), s.PendingResponseCount.ToString(), s.AcceptedCount.ToString(),
                                s.InProgressCount.ToString(), s.CompletedCount.ToString(), s.DeclinedCount.ToString(),
                                s.OverdueCount.ToString(), $"{s.CompletionRate}%", Num(s.AverageResponseHours),
                            }).ToList(),
                            new[] { 2.4f, 0.8f, 0.9f, 0.8f, 0.9f, 0.7f, 0.9f, 1f, 1f, 1f });
                    }

                    if (sections.Contains("HANDOVER_SUMMARY"))
                    {
                        SectionTitle($"4. Bàn giao / ký nhận (top {o.HandoverSummary.Count}/{o.HandoverTotal})");
                        DataTable(
                            new[] { "Item", "Visit", "Loại", "Mượn ký", "Giao ký", "Tình trạng", "Trạng thái" },
                            o.HandoverSummary.Select(h => new[]
                            {
                                h.ItemName, h.VisitCode, h.HandoverType == "BORROW" ? "Mượn" : "Trả",
                                h.BorrowerSigned ? "Có" : "Chưa", h.ProviderSigned ? "Có" : "Chưa",
                                h.ItemCondition ?? "—", h.StatusLabel,
                            }).ToList(),
                            new[] { 2.4f, 1.4f, 0.8f, 0.9f, 0.9f, 1.1f, 1.8f });
                    }

                    if (sections.Contains("INCIDENT_SUMMARY"))
                    {
                        SectionTitle("5. Phát sinh sau bàn giao");
                        DataTable(
                            new[] { "Mảng việc", "Tổng SL", "Hư hỏng", "Thiếu/mất", "Cần xử lý", "Ghi chú mới nhất" },
                            o.IncidentSummary.Select(i => new[]
                            {
                                i.ItemTypeLabelVi, i.TotalQuantity.ToString(), i.DamagedCount.ToString(),
                                i.MissingCount.ToString(), i.NeedActionCount.ToString(), i.LatestNote ?? "",
                            }).ToList(),
                            new[] { 2f, 0.9f, 0.9f, 1f, 1f, 3f });
                    }

                    if (sections.Contains("FEEDBACK_SUMMARY"))
                    {
                        var fb = o.FeedbackSummary;
                        SectionTitle("6. Feedback về phòng ban / hậu cần");
                        KeyValueTable(new List<(string, string)>
                        {
                            ("Điểm trung bình", Num(fb.AverageRating)),
                            ("Tổng feedback", fb.TotalFeedbacks.ToString()),
                            ("Feedback thấp (≤2)", fb.LowFeedbackCount.ToString()),
                        });
                        if (fb.FeedbackByItemType.Count > 0)
                        {
                            SubTitle("Đánh giá theo mảng việc");
                            DataTable(
                                new[] { "Mảng việc", "Điểm TB", "Số feedback" },
                                fb.FeedbackByItemType.Select(t => new[] { t.LabelVi, Num(t.AverageRating), t.FeedbackCount.ToString() }).ToList(),
                                new[] { 3f, 1f, 1f });
                        }
                        SubTitle("Feedback thấp cần chú ý");
                        DataTable(
                            new[] { "Đoàn", "Đối tượng", "Rating", "Nội dung", "Ngày gửi" },
                            fb.LowRatedItems.Select(e => new[]
                            {
                                e.DelegationName, e.ItemName ?? "—", e.Rating.ToString(), e.Comment ?? "", Dt(e.SubmittedAt),
                            }).ToList(),
                            new[] { 2f, 2f, 0.7f, 3f, 1.1f });
                        SubTitle("Feedback gần đây");
                        DataTable(
                            new[] { "Đoàn", "Đối tượng", "Rating", "Nội dung", "Ngày gửi" },
                            fb.RecentFeedbacks.Select(e => new[]
                            {
                                e.DelegationName, e.ItemName ?? "—", e.Rating.ToString(), e.Comment ?? "", Dt(e.SubmittedAt),
                            }).ToList(),
                            new[] { 2f, 2f, 0.7f, 3f, 1.1f });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(muted));
                    t.Span("PEMS · Department Leader Report · Trang ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}

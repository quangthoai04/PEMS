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
using PEMS.Application.Common;
using PEMS.Application.Reports.Queries.GetDeptLeaderReportV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Reports.Commands.ExportDeptLeaderReportV2;

/// <summary>
/// Dựng file báo cáo phòng ban (CSV / Excel / PDF) từ đúng aggregation của
/// GET dept-leader-report-v2 — guard role/scope nằm trong query handler.
/// </summary>
public sealed class ExportDeptLeaderReportV2CommandHandler
    : IRequestHandler<ExportDeptLeaderReportV2Command, ExportDeptLeaderReportV2Result>
{
    private const string BrandBlue = "#004C91";
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly string[] AllSections = { "TASKS", "PERSONNEL" };

    private readonly IMediator _mediator;

    public ExportDeptLeaderReportV2CommandHandler(IMediator mediator)
    {
        _mediator = mediator;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ExportDeptLeaderReportV2Result> Handle(ExportDeptLeaderReportV2Command request, CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetDeptLeaderReportV2Query
        {
            Preset = request.Preset,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
        }, cancellationToken);

        var sections = request.Sections is { Length: > 0 }
            ? request.Sections.Select(s => s.Trim().ToUpperInvariant()).Where(s => AllSections.Contains(s)).ToHashSet()
            : AllSections.ToHashSet();
        if (sections.Count == 0) sections = AllSections.ToHashSet();

        var format = request.ExportFormat?.Trim().ToUpperInvariant() switch
        {
            "PDF" => "PDF",
            "CSV" => "CSV",
            _ => "EXCEL",
        };
        var baseName = $"PEMS_BaoCao_PhongBan_{VietnamTime.Now():yyyyMMdd_HHmm}";

        return format switch
        {
            "CSV" => new ExportDeptLeaderReportV2Result
            {
                Content = BuildCsv(data, sections),
                ContentType = "text/csv; charset=utf-8",
                FileName = baseName + ".csv",
            },
            "PDF" => new ExportDeptLeaderReportV2Result
            {
                Content = BuildPdf(data, sections),
                ContentType = "application/pdf",
                FileName = baseName + ".pdf",
            },
            _ => new ExportDeptLeaderReportV2Result
            {
                Content = BuildExcel(data, sections),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = baseName + ".xlsx",
            },
        };
    }

    private static string RoleLabel(string role) => role == "DEPT_LEADER" ? "Department Leader ★" : "Dept Staff";
    private static string Fb(double? avg, int count) => avg != null ? $"{avg.Value.ToString("0.0", Vi)}★ ({count})" : "—";

    // ─────────────────────────────── CSV ───────────────────────────────
    private static byte[] BuildCsv(DeptLeaderReportV2Dto d, HashSet<string> sections)
    {
        var sb = new StringBuilder();
        string Esc(string? s) => $"\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";
        sb.AppendLine($"Báo cáo phòng ban;{Esc(d.DepartmentName)};Kỳ;{d.FromDate};{d.ToDate}");
        sb.AppendLine();

        if (sections.Contains("TASKS"))
        {
            var t = d.Tasks;
            sb.AppendLine("1. BÁO CÁO NHIỆM VỤ");
            sb.AppendLine("Tổng NV;Hoàn thành;Từ chối;Chưa hoàn thành;Tổng sao;Lượt FB;FB TB");
            sb.AppendLine($"{t.TotalTasks};{t.Completed};{t.Rejected};{t.NotCompleted};{t.FeedbackTotalStars};{t.FeedbackCount};{t.FeedbackAverage?.ToString("0.0", Vi) ?? "—"}");
            sb.AppendLine("Mốc thời gian;Tổng nhiệm vụ;Hoàn thành");
            foreach (var pt in t.Trend)
                sb.AppendLine($"{Esc(pt.MonthLabel)};{pt.TotalTasks};{pt.Completed}");
            sb.AppendLine();
        }

        if (sections.Contains("PERSONNEL"))
        {
            var p = d.Personnel;
            sb.AppendLine("2. BÁO CÁO NHÂN SỰ");
            sb.AppendLine($"Tổng nhân sự;{p.TotalStaff};Feedback TB;{p.AverageFeedback?.ToString("0.0", Vi) ?? "—"}");
            sb.AppendLine("STT;Tên;Vai trò;Số nhiệm vụ phụ trách;Tổng giờ làm việc;Feedback;Từ chối");
            var i = 0;
            foreach (var r in p.Rows)
                sb.AppendLine($"{++i};{Esc(r.FullName)};{RoleLabel(r.Role)};{r.TaskCount};{r.TotalHours.ToString("0.#", Vi)};{Fb(r.FeedbackAverage, r.FeedbackCount)};{r.DeclinedCount}");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // ─────────────────────────────── Excel ───────────────────────────────
    private static byte[] BuildExcel(DeptLeaderReportV2Dto d, HashSet<string> sections)
    {
        using var workbook = new XLWorkbook();

        void Head(IXLWorksheet ws, int row, params string[] cols)
        {
            for (var c = 0; c < cols.Length; c++)
            {
                var cell = ws.Cell(row, c + 1);
                cell.Value = cols[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
            }
        }

        if (sections.Contains("TASKS"))
        {
            var ws = workbook.AddWorksheet("1. Nhiệm vụ");
            var t = d.Tasks;
            ws.Cell(1, 1).Value = $"Báo cáo nhiệm vụ — {d.DepartmentName} ({d.FromDate} → {d.ToDate})";
            ws.Cell(1, 1).Style.Font.Bold = true;
            Head(ws, 3, "Tổng NV", "Hoàn thành", "Từ chối", "Chưa hoàn thành", "Tổng sao", "Lượt FB", "FB TB");
            ws.Cell(4, 1).Value = t.TotalTasks; ws.Cell(4, 2).Value = t.Completed; ws.Cell(4, 3).Value = t.Rejected;
            ws.Cell(4, 4).Value = t.NotCompleted; ws.Cell(4, 5).Value = t.FeedbackTotalStars; ws.Cell(4, 6).Value = t.FeedbackCount;
            ws.Cell(4, 7).Value = t.FeedbackAverage?.ToString("0.0", Vi) ?? "—";
            Head(ws, 6, "Mốc thời gian", "Tổng nhiệm vụ", "Hoàn thành");
            var row = 7;
            foreach (var pt in t.Trend)
            {
                ws.Cell(row, 1).Value = pt.MonthLabel; ws.Cell(row, 2).Value = pt.TotalTasks; ws.Cell(row, 3).Value = pt.Completed;
                row++;
            }
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("PERSONNEL"))
        {
            var ws = workbook.AddWorksheet("2. Nhân sự");
            var p = d.Personnel;
            ws.Cell(1, 1).Value = $"Báo cáo nhân sự — tổng {p.TotalStaff}, feedback TB {p.AverageFeedback?.ToString("0.0", Vi) ?? "—"}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            Head(ws, 3, "STT", "Tên", "Email", "Vai trò", "Số nhiệm vụ", "Tổng giờ làm việc", "Feedback", "Từ chối");
            var row = 4; var i = 0;
            foreach (var r in p.Rows)
            {
                ws.Cell(row, 1).Value = ++i; ws.Cell(row, 2).Value = r.FullName; ws.Cell(row, 3).Value = r.Email;
                ws.Cell(row, 4).Value = RoleLabel(r.Role); ws.Cell(row, 5).Value = r.TaskCount;
                ws.Cell(row, 6).Value = r.TotalHours; ws.Cell(row, 7).Value = Fb(r.FeedbackAverage, r.FeedbackCount);
                ws.Cell(row, 8).Value = r.DeclinedCount;
                if (r.FeedbackAverage is < 2) ws.Row(row).Style.Font.FontColor = XLColor.Red;
                row++;
            }
            ws.Columns().AdjustToContents();
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─────────────────────────────── PDF ───────────────────────────────
    private static byte[] BuildPdf(DeptLeaderReportV2Dto d, HashSet<string> sections)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, QuestPDF.Infrastructure.Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("BÁO CÁO PHÒNG BAN — DEPARTMENT LEADER").Bold().FontSize(16).FontColor(BrandBlue);
                    col.Item().Text($"{d.DepartmentName} · Kỳ {d.FromDate} → {d.ToDate} · Lập lúc {d.GeneratedAt:HH:mm dd/MM/yyyy}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(14);

                    void TableHeader(IContainer c, string text) =>
                        c.Background(BrandBlue).Padding(4).Text(text).Bold().FontSize(9).FontColor(Colors.White);
                    void Cell(IContainer c, string text) =>
                        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text).FontSize(9);

                    if (sections.Contains("TASKS"))
                    {
                        var t = d.Tasks;
                        col.Item().Text("1 · BÁO CÁO NHIỆM VỤ").Bold().FontSize(12).FontColor(BrandBlue);
                        col.Item().Text(
                            $"Tổng nhiệm vụ: {t.TotalTasks} · Hoàn thành: {t.Completed} · Từ chối: {t.Rejected} · "
                            + $"Chưa hoàn thành: {t.NotCompleted} · Feedback: {Fb(t.FeedbackAverage, t.FeedbackCount)} ({t.FeedbackTotalStars} sao)")
                            .FontSize(9.5f);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h =>
                            {
                                TableHeader(h.Cell(), "Mốc thời gian");
                                TableHeader(h.Cell(), "Tổng nhiệm vụ");
                                TableHeader(h.Cell(), "Hoàn thành");
                            });
                            foreach (var pt in t.Trend)
                            {
                                Cell(table.Cell(), pt.MonthLabel);
                                Cell(table.Cell(), pt.TotalTasks.ToString(Vi));
                                Cell(table.Cell(), pt.Completed.ToString(Vi));
                            }
                        });
                    }

                    if (sections.Contains("PERSONNEL"))
                    {
                        var p = d.Personnel;
                        col.Item().Text("2 · BÁO CÁO NHÂN SỰ").Bold().FontSize(12).FontColor(BrandBlue);
                        col.Item().Text($"Tổng nhân sự: {p.TotalStaff} · Feedback TB: {p.AverageFeedback?.ToString("0.0", Vi) ?? "—"}")
                            .FontSize(9.5f);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28); c.RelativeColumn(3); c.RelativeColumn(1.5f);
                                c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(1.2f); c.RelativeColumn(0.9f);
                            });
                            table.Header(h =>
                            {
                                TableHeader(h.Cell(), "STT"); TableHeader(h.Cell(), "Tên"); TableHeader(h.Cell(), "Vai trò");
                                TableHeader(h.Cell(), "Số NV"); TableHeader(h.Cell(), "Tổng giờ"); TableHeader(h.Cell(), "Feedback"); TableHeader(h.Cell(), "Từ chối");
                            });
                            var i = 0;
                            foreach (var r in p.Rows)
                            {
                                Cell(table.Cell(), (++i).ToString(Vi));
                                Cell(table.Cell(), r.FullName);
                                Cell(table.Cell(), RoleLabel(r.Role));
                                Cell(table.Cell(), r.TaskCount.ToString(Vi));
                                Cell(table.Cell(), r.TotalHours.ToString("0.#", Vi));
                                Cell(table.Cell(), Fb(r.FeedbackAverage, r.FeedbackCount) + (r.FeedbackAverage is < 2 ? " ⚠" : ""));
                                Cell(table.Cell(), r.DeclinedCount.ToString(Vi));
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("PEMS — Partnership Engagement Management System · Trang ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });
        return doc.GeneratePdf();
    }
}

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
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Reports.Queries.GetHoReportV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Reports.Commands.ExportHoReportV2;

/// <summary>
/// Dựng file báo cáo hệ thống HO (CSV / Excel / PDF) từ đúng aggregation của
/// GET ho-report-v2 — guard role nằm trong query handler.
/// </summary>
public sealed class ExportHoReportV2CommandHandler
    : IRequestHandler<ExportHoReportV2Command, ExportHoReportV2Result>
{
    private const string BrandBlue = "#004C91";
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly string[] AllSections = { "OVERVIEW", "PARTNERS" };

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly Reports.Common.IReportArchiveService _reportArchive;

    public ExportHoReportV2CommandHandler(
        IMediator mediator, ICurrentUserService currentUser, Reports.Common.IReportArchiveService reportArchive)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _reportArchive = reportArchive;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ExportHoReportV2Result> Handle(ExportHoReportV2Command request, CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetHoReportV2Query
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
        var baseName = $"PEMS_BaoCao_HeThong_{VietnamTime.Now():yyyyMMdd_HHmm}";

        var result = format switch
        {
            "CSV" => new ExportHoReportV2Result
            {
                Content = BuildCsv(data, sections),
                ContentType = "text/csv; charset=utf-8",
                FileName = baseName + ".csv",
            },
            "PDF" => new ExportHoReportV2Result
            {
                Content = BuildPdf(data, sections),
                ContentType = "application/pdf",
                FileName = baseName + ".pdf",
            },
            _ => new ExportHoReportV2Result
            {
                Content = BuildExcel(data, sections),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = baseName + ".xlsx",
            },
        };

        if (_currentUser.UserId is { } userId)
        {
            await _reportArchive.ArchiveAsync(
                result.Content, result.FileName, result.ContentType, "HO_REPORT_V2",
                _currentUser.PrimaryCampusId, userId, cancellationToken);
        }

        return result;
    }

    private static string Fb(double? avg, int count) => avg != null ? $"{avg.Value.ToString("0.0", Vi)}★ ({count})" : "—";

    // ─────────────────────────────── CSV ───────────────────────────────
    private static byte[] BuildCsv(HoReportV2Dto d, HashSet<string> sections)
    {
        var sb = new StringBuilder();
        string Esc(string? s) => $"\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";
        sb.AppendLine($"Báo cáo hệ thống PEMS;Kỳ;{d.FromDate};{d.ToDate}");
        sb.AppendLine();

        if (sections.Contains("OVERVIEW"))
        {
            var o = d.Overview;
            sb.AppendLine("1. TỔNG QUAN HỆ THỐNG");
            sb.AppendLine("Số campus;Tổng đoàn;Tổng khách;Tổng đối tác;Đơn liên cơ sở;Đơn một cơ sở;Hoàn thành;Bị hủy;Từ chối;Feedback TB;Lượt FB");
            sb.AppendLine($"{o.CampusCount};{o.TotalVisits};{o.TotalGuests};{o.TotalPartners};{o.MultiCampusRequests};{o.SingleCampusRequests};{o.Completed};{o.Cancelled};{o.Rejected};{o.FeedbackAverage?.ToString("0.0", Vi) ?? "—"};{o.FeedbackCount}");
            sb.AppendLine("STT;Campus;Tổng đoàn khách;Tổng đối tác;Feedback");
            var i = 0;
            foreach (var r in o.CampusRows)
                sb.AppendLine($"{++i};{Esc(r.Name)};{r.TotalVisits};{r.TotalPartners};{Fb(r.FeedbackAverage, r.FeedbackCount)}");
            sb.AppendLine();
            sb.AppendLine("Mốc thời gian;" + string.Join(";", o.Campuses.Select(c => Esc(c.Name))));
            foreach (var t in o.Trend)
                sb.AppendLine($"{Esc(t.MonthLabel)};" + string.Join(";", o.Campuses.Select(c =>
                    t.ByCampus.TryGetValue(c.CampusId.ToString(CultureInfo.InvariantCulture), out var v) ? v : 0)));
            sb.AppendLine();
        }

        if (sections.Contains("PARTNERS"))
        {
            var p = d.Partners;
            sb.AppendLine("2. ĐỐI TÁC");
            sb.AppendLine("Mốc thời gian;Chuyến gắn đối tác;Đối tác mới;Lũy kế đối tác");
            foreach (var t in p.Trend)
                sb.AppendLine($"{Esc(t.MonthLabel)};{t.VisitsWithPartner};{t.NewPartners};{t.CumulativePartners}");
            sb.AppendLine("STT;Tên đối tác;Đất nước;Số lần tham quan;Feedback");
            var i = 0;
            foreach (var r in p.Rows)
                sb.AppendLine($"{++i};{Esc(r.Name)};{Esc(r.Country)};{r.VisitCount};{Fb(r.FeedbackAverage, r.FeedbackCount)}");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // ─────────────────────────────── Excel ───────────────────────────────
    private static byte[] BuildExcel(HoReportV2Dto d, HashSet<string> sections)
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

        if (sections.Contains("OVERVIEW"))
        {
            var ws = workbook.AddWorksheet("1. Tổng quan hệ thống");
            var o = d.Overview;
            ws.Cell(1, 1).Value = $"Tổng quan hệ thống PEMS ({d.FromDate} → {d.ToDate})";
            ws.Cell(1, 1).Style.Font.Bold = true;
            Head(ws, 3, "Số campus", "Tổng đoàn", "Tổng khách", "Tổng đối tác", "Đơn liên cơ sở", "Đơn một cơ sở", "Hoàn thành", "Bị hủy", "Từ chối", "FB TB", "Lượt FB");
            ws.Cell(4, 1).Value = o.CampusCount; ws.Cell(4, 2).Value = o.TotalVisits; ws.Cell(4, 3).Value = o.TotalGuests;
            ws.Cell(4, 4).Value = o.TotalPartners; ws.Cell(4, 5).Value = o.MultiCampusRequests; ws.Cell(4, 6).Value = o.SingleCampusRequests;
            ws.Cell(4, 7).Value = o.Completed; ws.Cell(4, 8).Value = o.Cancelled; ws.Cell(4, 9).Value = o.Rejected;
            ws.Cell(4, 10).Value = o.FeedbackAverage?.ToString("0.0", Vi) ?? "—"; ws.Cell(4, 11).Value = o.FeedbackCount;

            Head(ws, 6, "STT", "Campus", "Tổng đoàn khách", "Tổng đối tác", "Feedback");
            var row = 7; var i = 0;
            foreach (var r in o.CampusRows)
            {
                ws.Cell(row, 1).Value = ++i; ws.Cell(row, 2).Value = r.Name; ws.Cell(row, 3).Value = r.TotalVisits;
                ws.Cell(row, 4).Value = r.TotalPartners; ws.Cell(row, 5).Value = Fb(r.FeedbackAverage, r.FeedbackCount);
                if (r.FeedbackAverage is < 2) ws.Row(row).Style.Font.FontColor = XLColor.Red;
                row++;
            }

            row += 1;
            var headCols = new List<string> { "Mốc thời gian" };
            headCols.AddRange(o.Campuses.Select(c => c.Name));
            Head(ws, row, headCols.ToArray());
            row++;
            foreach (var t in o.Trend)
            {
                ws.Cell(row, 1).Value = t.MonthLabel;
                for (var c = 0; c < o.Campuses.Count; c++)
                {
                    var key = o.Campuses[c].CampusId.ToString(CultureInfo.InvariantCulture);
                    ws.Cell(row, c + 2).Value = t.ByCampus.TryGetValue(key, out var v) ? v : 0;
                }
                row++;
            }
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("PARTNERS"))
        {
            var ws = workbook.AddWorksheet("2. Đối tác");
            var p = d.Partners;
            ws.Cell(1, 1).Value = $"Đối tác toàn hệ thống ({d.FromDate} → {d.ToDate})";
            ws.Cell(1, 1).Style.Font.Bold = true;
            Head(ws, 3, "STT", "Tên đối tác", "Đất nước", "Số lần tham quan", "Feedback");
            var row = 4; var i = 0;
            foreach (var r in p.Rows)
            {
                ws.Cell(row, 1).Value = ++i; ws.Cell(row, 2).Value = r.Name; ws.Cell(row, 3).Value = r.Country ?? "—";
                ws.Cell(row, 4).Value = r.VisitCount; ws.Cell(row, 5).Value = Fb(r.FeedbackAverage, r.FeedbackCount);
                row++;
            }
            row += 1;
            Head(ws, row, "Mốc thời gian", "Chuyến gắn đối tác", "Đối tác mới", "Lũy kế đối tác");
            row++;
            foreach (var t in p.Trend)
            {
                ws.Cell(row, 1).Value = t.MonthLabel; ws.Cell(row, 2).Value = t.VisitsWithPartner;
                ws.Cell(row, 3).Value = t.NewPartners; ws.Cell(row, 4).Value = t.CumulativePartners;
                row++;
            }
            ws.Columns().AdjustToContents();
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─────────────────────────────── PDF ───────────────────────────────
    private static byte[] BuildPdf(HoReportV2Dto d, HashSet<string> sections)
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
                    col.Item().Text("BÁO CÁO HỆ THỐNG — HEAD OFFICE").Bold().FontSize(16).FontColor(BrandBlue);
                    col.Item().Text($"Toàn hệ thống PEMS · Kỳ {d.FromDate} → {d.ToDate} · Lập lúc {d.GeneratedAt:HH:mm dd/MM/yyyy}")
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

                    if (sections.Contains("OVERVIEW"))
                    {
                        var o = d.Overview;
                        col.Item().Text("1 · TỔNG QUAN HỆ THỐNG").Bold().FontSize(12).FontColor(BrandBlue);
                        col.Item().Text(
                            $"Số campus: {o.CampusCount} · Tổng đoàn: {o.TotalVisits} ({o.TotalGuests} khách) · Đối tác: {o.TotalPartners} · "
                            + $"Đơn liên cơ sở: {o.MultiCampusRequests} · Đơn một cơ sở: {o.SingleCampusRequests} · "
                            + $"Hoàn thành: {o.Completed} · Hủy: {o.Cancelled} · Từ chối: {o.Rejected} · Feedback TB: {Fb(o.FeedbackAverage, o.FeedbackCount)}")
                            .FontSize(9.5f);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28); c.RelativeColumn(3); c.RelativeColumn(1.4f); c.RelativeColumn(1.2f); c.RelativeColumn(1.4f);
                            });
                            table.Header(h =>
                            {
                                TableHeader(h.Cell(), "STT"); TableHeader(h.Cell(), "Campus"); TableHeader(h.Cell(), "Tổng đoàn khách");
                                TableHeader(h.Cell(), "Tổng đối tác"); TableHeader(h.Cell(), "Feedback");
                            });
                            var i = 0;
                            foreach (var r in o.CampusRows)
                            {
                                Cell(table.Cell(), (++i).ToString(Vi));
                                Cell(table.Cell(), r.Name);
                                Cell(table.Cell(), r.TotalVisits.ToString(Vi));
                                Cell(table.Cell(), r.TotalPartners.ToString(Vi));
                                Cell(table.Cell(), Fb(r.FeedbackAverage, r.FeedbackCount) + (r.FeedbackAverage is < 2 ? " ⚠" : ""));
                            }
                        });
                    }

                    if (sections.Contains("PARTNERS"))
                    {
                        var p = d.Partners;
                        col.Item().Text("2 · ĐỐI TÁC (xếp theo số lượt tham quan)").Bold().FontSize(12).FontColor(BrandBlue);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28); c.RelativeColumn(3); c.RelativeColumn(1.4f); c.RelativeColumn(1.4f); c.RelativeColumn(1.4f);
                            });
                            table.Header(h =>
                            {
                                TableHeader(h.Cell(), "STT"); TableHeader(h.Cell(), "Tên đối tác"); TableHeader(h.Cell(), "Đất nước");
                                TableHeader(h.Cell(), "Số lần tham quan"); TableHeader(h.Cell(), "Feedback");
                            });
                            var i = 0;
                            foreach (var r in p.Rows)
                            {
                                Cell(table.Cell(), (++i).ToString(Vi));
                                Cell(table.Cell(), r.Name);
                                Cell(table.Cell(), r.Country ?? "—");
                                Cell(table.Cell(), r.VisitCount.ToString(Vi));
                                Cell(table.Cell(), Fb(r.FeedbackAverage, r.FeedbackCount));
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

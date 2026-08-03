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
using PEMS.Application.Reports.Queries.GetStaffLeaderReportV2;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Reports.Commands.ExportStaffLeaderReportV2;

/// <summary>
/// Dựng file báo cáo 3 phần (CSV / Excel qua ClosedXML / PDF qua QuestPDF) từ đúng
/// aggregation của GET staff-leader-report-v2 — guard role/campus nằm trong query handler.
/// </summary>
public sealed class ExportStaffLeaderReportV2CommandHandler
    : IRequestHandler<ExportStaffLeaderReportV2Command, ExportStaffLeaderReportV2Result>
{
    private const string BrandBlue = "#004C91";
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly string[] AllSections = { "VISITS", "PARTNERS", "PERSONNEL", "DEPARTMENTS", "EXPENSES" };

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly Reports.Common.IReportArchiveService _reportArchive;

    public ExportStaffLeaderReportV2CommandHandler(
        IMediator mediator, ICurrentUserService currentUser, Reports.Common.IReportArchiveService reportArchive)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _reportArchive = reportArchive;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ExportStaffLeaderReportV2Result> Handle(ExportStaffLeaderReportV2Command request, CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetStaffLeaderReportV2Query
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
        var baseName = $"PEMS_BaoCao_Campus_{VietnamTime.Now():yyyyMMdd_HHmm}";

        var result = format switch
        {
            "CSV" => new ExportStaffLeaderReportV2Result
            {
                Content = BuildCsv(data, sections),
                ContentType = "text/csv; charset=utf-8",
                FileName = baseName + ".csv",
            },
            "PDF" => new ExportStaffLeaderReportV2Result
            {
                Content = BuildPdf(data, sections),
                ContentType = "application/pdf",
                FileName = baseName + ".pdf",
            },
            _ => new ExportStaffLeaderReportV2Result
            {
                Content = BuildExcel(data, sections),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = baseName + ".xlsx",
            },
        };

        if (_currentUser.UserId is { } userId)
        {
            await _reportArchive.ArchiveAsync(
                result.Content, result.FileName, result.ContentType, "STAFF_LEADER_REPORT_V2",
                _currentUser.PrimaryCampusId, userId, cancellationToken);
        }

        return result;
    }

    private static string RoleLabel(string role) => role switch
    {
        "STAFF_LEADER" => "Staff Leader ★",
        "STAFF" => "Staff",
        _ => "Student",
    };

    private static string Fb(double? avg, int count) => avg != null ? $"{avg.Value.ToString("0.0", Vi)}★ ({count})" : "—";

    // ─────────────────────────────── CSV ───────────────────────────────
    private static byte[] BuildCsv(StaffLeaderReportV2Dto d, HashSet<string> sections)
    {
        var sb = new StringBuilder();
        string Esc(string? s) => $"\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";
        sb.AppendLine($"Báo cáo campus;{Esc(d.CampusName)};Kỳ;{d.FromDate};{d.ToDate}");
        sb.AppendLine();

        if (sections.Contains("VISITS"))
        {
            var v = d.Visits;
            sb.AppendLine("1. BÁO CÁO ĐOÀN TIẾP KHÁCH");
            sb.AppendLine("Tổng đoàn;Tổng khách;Hoàn thành;Từ chối;Bị hủy;Chưa hoàn thành;Tổng sao;Lượt feedback;Feedback TB;Tổng đối tác");
            sb.AppendLine($"{v.TotalVisits};{v.TotalGuests};{v.Completed};{v.Rejected};{v.Cancelled};{v.NotCompleted};{v.FeedbackTotalStars};{v.FeedbackCount};{v.FeedbackAverage?.ToString("0.0", Vi) ?? "—"};{v.TotalPartners}");
            sb.AppendLine("Mốc thời gian;Chuyến gắn đối tác;Đối tác mới;Lũy kế đối tác");
            foreach (var t in v.PartnerTrend)
                sb.AppendLine($"{Esc(t.MonthLabel)};{t.VisitsWithPartner};{t.NewPartners};{t.CumulativePartners}");
            sb.AppendLine();
        }

        if (sections.Contains("PARTNERS"))
        {
            var pt = d.Partners;
            sb.AppendLine("2. BÁO CÁO ĐỐI TÁC");
            sb.AppendLine($"Tổng đối tác;{pt.TotalPartners};Đối tác mới trong kỳ;{pt.NewPartnersInPeriod};Đang hợp tác;{pt.ActivePartners};Đoàn có đối tác;{pt.VisitsWithPartnerCount};Tỷ lệ đoàn có đối tác;{pt.PartnerVisitRatio}%");
            sb.AppendLine("STT;Mã đối tác;Tên đối tác;Loại hình;Trạng thái hợp tác;Số chuyến thăm;Số khách");
            var i = 0;
            foreach (var r in pt.TopPartners)
                sb.AppendLine($"{++i};{Esc(r.PartnerCode ?? "—")};{Esc(r.Name)};{Esc(r.PartnerType)};{Esc(r.CooperationStatus)};{r.VisitCount};{r.GuestCount}");
            sb.AppendLine();
        }

        if (sections.Contains("PERSONNEL"))
        {
            var p = d.Personnel;
            sb.AppendLine("3. BÁO CÁO NHÂN SỰ");
            sb.AppendLine($"Tổng nhân sự;{p.TotalStaff};Tổng student;{p.TotalStudents};Feedback TB;{p.AverageFeedback?.ToString("0.0", Vi) ?? "—"}");
            sb.AppendLine("STT;Tên;Vai trò;Số đoàn phụ trách;Tổng giờ làm việc;Feedback;Từ chối");
            var i = 0;
            foreach (var r in p.Rows)
                sb.AppendLine($"{++i};{Esc(r.FullName)};{RoleLabel(r.Role)};{r.VisitCount};{r.TotalHours.ToString("0.#", Vi)};{Fb(r.FeedbackAverage, r.FeedbackCount)};{r.DeclinedCount}");
            sb.AppendLine();
        }

        if (sections.Contains("DEPARTMENTS"))
        {
            var dep = d.Departments;
            sb.AppendLine("4. BÁO CÁO PHÒNG BAN KHÁC");
            sb.AppendLine($"Tổng phòng ban;{dep.TotalDepartments};Hoàn thành;{dep.CompletedTotal};Từ chối;{dep.RejectedTotal};Feedback TB;{dep.AverageFeedback?.ToString("0.0", Vi) ?? "—"}");
            sb.AppendLine("STT;Tên phòng ban;Tổng đơn/thư;Hoàn thành;Từ chối;Feedback");
            var i = 0;
            foreach (var r in dep.Rows)
                sb.AppendLine($"{++i};{Esc(r.Name)};{r.TotalRequests};{r.Completed};{r.Rejected};{Fb(r.FeedbackAverage, r.FeedbackCount)}");
            sb.AppendLine();
        }

        if (sections.Contains("EXPENSES"))
        {
            var ex = d.Expenses;
            sb.AppendLine("5. THỐNG KÊ CHI PHÍ");
            sb.AppendLine($"Tổng chi phí;{ex.TotalAmount};Host;{ex.TotalGeneral};Hậu cần PB;{ex.TotalLogistics}");
            sb.AppendLine("STT;Tên đoàn khách;Thời gian;Chi phí Host;Chi phí Hậu cần;Tổng chi phí;Trạng thái");
            var i = 0;
            foreach (var r in ex.Rows)
                sb.AppendLine($"{++i};{Esc(r.DelegationName)};{r.VisitDate:dd/MM/yyyy};{r.GeneralExpense};{r.LogisticsExpense};{r.TotalExpense};{Esc(r.Status)}");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // ─────────────────────────────── Excel ───────────────────────────────
    private static byte[] BuildExcel(StaffLeaderReportV2Dto d, HashSet<string> sections)
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

        if (sections.Contains("VISITS"))
        {
            var ws = workbook.AddWorksheet("1. Đoàn tiếp khách");
            var v = d.Visits;
            ws.Cell(1, 1).Value = $"Báo cáo đoàn tiếp khách — {d.CampusName} ({d.FromDate} → {d.ToDate})";
            ws.Cell(1, 1).Style.Font.Bold = true;
            Head(ws, 3, "Tổng đoàn", "Tổng khách", "Hoàn thành", "Từ chối", "Bị hủy", "Chưa hoàn thành", "Tổng sao", "Lượt FB", "FB TB", "Tổng đối tác");
            ws.Cell(4, 1).Value = v.TotalVisits; ws.Cell(4, 2).Value = v.TotalGuests; ws.Cell(4, 3).Value = v.Completed;
            ws.Cell(4, 4).Value = v.Rejected; ws.Cell(4, 5).Value = v.Cancelled; ws.Cell(4, 6).Value = v.NotCompleted;
            ws.Cell(4, 7).Value = v.FeedbackTotalStars; ws.Cell(4, 8).Value = v.FeedbackCount;
            ws.Cell(4, 9).Value = v.FeedbackAverage?.ToString("0.0", Vi) ?? "—"; ws.Cell(4, 10).Value = v.TotalPartners;
            Head(ws, 6, "Mốc thời gian", "Chuyến gắn đối tác", "Đối tác mới", "Lũy kế đối tác");
            var row = 7;
            foreach (var t in v.PartnerTrend)
            {
                ws.Cell(row, 1).Value = t.MonthLabel; ws.Cell(row, 2).Value = t.VisitsWithPartner;
                ws.Cell(row, 3).Value = t.NewPartners; ws.Cell(row, 4).Value = t.CumulativePartners;
                row++;
            }
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("PARTNERS"))
        {
            var ws = workbook.AddWorksheet("2. Đối tác");
            var pt = d.Partners;
            ws.Cell(1, 1).Value = $"Báo cáo đối tác — {d.CampusName} ({d.FromDate} → {d.ToDate})";
            ws.Cell(1, 1).Style.Font.Bold = true;
            Head(ws, 3, "Tổng đối tác", "Đối tác mới trong kỳ", "Đang hợp tác", "Đoàn có đối tác", "Tỷ lệ đoàn có đối tác (%)");
            ws.Cell(4, 1).Value = pt.TotalPartners; ws.Cell(4, 2).Value = pt.NewPartnersInPeriod;
            ws.Cell(4, 3).Value = pt.ActivePartners; ws.Cell(4, 4).Value = pt.VisitsWithPartnerCount;
            ws.Cell(4, 5).Value = pt.PartnerVisitRatio;

            Head(ws, 6, "STT", "Mã đối tác", "Tên đối tác", "Loại hình", "Trạng thái hợp tác", "Số chuyến thăm", "Số khách");
            var row = 7; var i = 0;
            foreach (var r in pt.TopPartners)
            {
                ws.Cell(row, 1).Value = ++i; ws.Cell(row, 2).Value = r.PartnerCode ?? "—";
                ws.Cell(row, 3).Value = r.Name; ws.Cell(row, 4).Value = r.PartnerType;
                ws.Cell(row, 5).Value = r.CooperationStatus; ws.Cell(row, 6).Value = r.VisitCount;
                ws.Cell(row, 7).Value = r.GuestCount;
                row++;
            }
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("PERSONNEL"))
        {
            var ws = workbook.AddWorksheet("3. Nhân sự");
            var p = d.Personnel;
            ws.Cell(1, 1).Value = $"Báo cáo nhân sự — tổng nhân sự {p.TotalStaff}, student {p.TotalStudents}, feedback TB {p.AverageFeedback?.ToString("0.0", Vi) ?? "—"}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            Head(ws, 3, "STT", "Tên", "Email", "Vai trò", "Số đoàn phụ trách", "Tổng giờ làm việc", "Feedback", "Từ chối");
            var row = 4; var i = 0;
            foreach (var r in p.Rows)
            {
                ws.Cell(row, 1).Value = ++i; ws.Cell(row, 2).Value = r.FullName; ws.Cell(row, 3).Value = r.Email;
                ws.Cell(row, 4).Value = RoleLabel(r.Role); ws.Cell(row, 5).Value = r.VisitCount;
                ws.Cell(row, 6).Value = r.TotalHours; ws.Cell(row, 7).Value = Fb(r.FeedbackAverage, r.FeedbackCount);
                ws.Cell(row, 8).Value = r.DeclinedCount;
                if (r.FeedbackAverage is < 2) ws.Row(row).Style.Font.FontColor = XLColor.Red;
                row++;
            }
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("DEPARTMENTS"))
        {
            var ws = workbook.AddWorksheet("4. Phòng ban khác");
            var dep = d.Departments;
            ws.Cell(1, 1).Value = $"Báo cáo phòng ban — tổng {dep.TotalDepartments}, hoàn thành {dep.CompletedTotal}, từ chối {dep.RejectedTotal}, feedback TB {dep.AverageFeedback?.ToString("0.0", Vi) ?? "—"}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            Head(ws, 3, "STT", "Tên phòng ban", "Tổng đơn/thư", "Hoàn thành", "Từ chối", "Feedback");
            var row = 4; var i = 0;
            foreach (var r in dep.Rows)
            {
                ws.Cell(row, 1).Value = ++i; ws.Cell(row, 2).Value = r.Name; ws.Cell(row, 3).Value = r.TotalRequests;
                ws.Cell(row, 4).Value = r.Completed; ws.Cell(row, 5).Value = r.Rejected;
                ws.Cell(row, 6).Value = Fb(r.FeedbackAverage, r.FeedbackCount);
                row++;
            }
            ws.Columns().AdjustToContents();
        }

        if (sections.Contains("EXPENSES"))
        {
            var ws = workbook.AddWorksheet("5. Thống kê chi phí");
            var ex = d.Expenses;
            ws.Cell(1, 1).Value = $"Thống kê chi phí tiếp khách — {d.CampusName} ({d.FromDate} → {d.ToDate})";
            ws.Cell(1, 1).Style.Font.Bold = true;
            Head(ws, 3, "Tổng chi phí", "Chi phí Host", "Chi phí Hậu cần PB");
            ws.Cell(4, 1).Value = ex.TotalAmount; ws.Cell(4, 2).Value = ex.TotalGeneral; ws.Cell(4, 3).Value = ex.TotalLogistics;

            Head(ws, 6, "STT", "Tên đoàn khách", "Thời gian", "Chi phí Host", "Chi phí Hậu cần", "Tổng chi phí", "Trạng thái");
            var row = 7; var i = 0;
            foreach (var r in ex.Rows)
            {
                ws.Cell(row, 1).Value = ++i; ws.Cell(row, 2).Value = r.DelegationName;
                ws.Cell(row, 3).Value = r.VisitDate?.ToString("dd/MM/yyyy") ?? "—";
                ws.Cell(row, 4).Value = r.GeneralExpense; ws.Cell(row, 5).Value = r.LogisticsExpense;
                ws.Cell(row, 6).Value = r.TotalExpense; ws.Cell(row, 7).Value = r.Status;
                row++;
            }
            ws.Columns().AdjustToContents();
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─────────────────────────────── PDF ───────────────────────────────
    private static byte[] BuildPdf(StaffLeaderReportV2Dto d, HashSet<string> sections)
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
                    col.Item().Text("BÁO CÁO CAMPUS — STAFF LEADER").Bold().FontSize(16).FontColor(BrandBlue);
                    col.Item().Text($"{d.CampusName} · Kỳ {d.FromDate} → {d.ToDate} · Lập lúc {d.GeneratedAt:HH:mm dd/MM/yyyy}")
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

                    if (sections.Contains("VISITS"))
                    {
                        var v = d.Visits;
                        col.Item().Text("1 · BÁO CÁO ĐOÀN TIẾP KHÁCH").Bold().FontSize(12).FontColor(BrandBlue);
                        col.Item().Text(
                            $"Tổng đoàn: {v.TotalVisits} ({v.TotalGuests} khách) · Hoàn thành: {v.Completed} · Từ chối: {v.Rejected} (+{v.Cancelled} hủy) · "
                            + $"Chưa hoàn thành: {v.NotCompleted} · Feedback: {Fb(v.FeedbackAverage, v.FeedbackCount)} ({v.FeedbackTotalStars} sao)")
                            .FontSize(9.5f);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h =>
                            {
                                TableHeader(h.Cell(), "Mốc thời gian");
                                TableHeader(h.Cell(), "Tổng số đoàn");
                                TableHeader(h.Cell(), "Đoàn hoàn thành");
                                TableHeader(h.Cell(), "Tổng số khách");
                            });
                            foreach (var t in v.VisitTrend)
                            {
                                Cell(table.Cell(), t.MonthLabel);
                                Cell(table.Cell(), t.TotalVisits.ToString(Vi));
                                Cell(table.Cell(), t.CompletedVisits.ToString(Vi));
                                Cell(table.Cell(), t.TotalGuests.ToString(Vi));
                            }
                        });
                    }

                    if (sections.Contains("PARTNERS"))
                    {
                        var pt = d.Partners;
                        col.Item().Text("2 · BÁO CÁO ĐỐI TÁC").Bold().FontSize(12).FontColor(BrandBlue);
                        col.Item().Text(
                            $"Tổng đối tác: {pt.TotalPartners} · Đối tác mới trong kỳ: {pt.NewPartnersInPeriod} · Đang hợp tác: {pt.ActivePartners} · "
                            + $"Đoàn có đối tác: {pt.VisitsWithPartnerCount} ({pt.PartnerVisitRatio}%)")
                            .FontSize(9.5f);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28); c.RelativeColumn(1.8f); c.RelativeColumn(3);
                                c.RelativeColumn(2); c.RelativeColumn(1.8f); c.RelativeColumn(1.2f); c.RelativeColumn(1.2f);
                            });
                            table.Header(h =>
                            {
                                TableHeader(h.Cell(), "STT"); TableHeader(h.Cell(), "Mã ĐT"); TableHeader(h.Cell(), "Tên đối tác");
                                TableHeader(h.Cell(), "Loại hình"); TableHeader(h.Cell(), "Trạng thái"); TableHeader(h.Cell(), "Số chuyến"); TableHeader(h.Cell(), "Số khách");
                            });
                            var i = 0;
                            foreach (var r in pt.TopPartners)
                            {
                                Cell(table.Cell(), (++i).ToString(Vi));
                                Cell(table.Cell(), r.PartnerCode ?? "—");
                                Cell(table.Cell(), r.Name);
                                Cell(table.Cell(), r.PartnerType);
                                Cell(table.Cell(), r.CooperationStatus);
                                Cell(table.Cell(), r.VisitCount.ToString(Vi));
                                Cell(table.Cell(), r.GuestCount.ToString(Vi));
                            }
                        });
                    }

                    if (sections.Contains("PERSONNEL"))
                    {
                        var p = d.Personnel;
                        col.Item().Text("3 · BÁO CÁO NHÂN SỰ").Bold().FontSize(12).FontColor(BrandBlue);
                        col.Item().Text($"Tổng nhân sự: {p.TotalStaff} · Tổng student: {p.TotalStudents} · Feedback TB: {p.AverageFeedback?.ToString("0.0", Vi) ?? "—"}")
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
                                TableHeader(h.Cell(), "Số đoàn"); TableHeader(h.Cell(), "Tổng giờ"); TableHeader(h.Cell(), "Feedback"); TableHeader(h.Cell(), "Từ chối");
                            });
                            var i = 0;
                            foreach (var r in p.Rows)
                            {
                                Cell(table.Cell(), (++i).ToString(Vi));
                                Cell(table.Cell(), r.FullName);
                                Cell(table.Cell(), RoleLabel(r.Role));
                                Cell(table.Cell(), r.VisitCount.ToString(Vi));
                                Cell(table.Cell(), r.TotalHours.ToString("0.#", Vi));
                                Cell(table.Cell(), Fb(r.FeedbackAverage, r.FeedbackCount) + (r.FeedbackAverage is < 2 ? " ⚠" : ""));
                                Cell(table.Cell(), r.DeclinedCount.ToString(Vi));
                            }
                        });
                    }

                    if (sections.Contains("DEPARTMENTS"))
                    {
                        var dep = d.Departments;
                        col.Item().Text("4 · BÁO CÁO PHÒNG BAN KHÁC").Bold().FontSize(12).FontColor(BrandBlue);
                        col.Item().Text($"Tổng phòng ban: {dep.TotalDepartments} · Hoàn thành: {dep.CompletedTotal} · Từ chối: {dep.RejectedTotal} · Feedback TB: {dep.AverageFeedback?.ToString("0.0", Vi) ?? "—"}")
                            .FontSize(9.5f);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28); c.RelativeColumn(3); c.RelativeColumn(1.2f);
                                c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(1.2f);
                            });
                            table.Header(h =>
                            {
                                TableHeader(h.Cell(), "STT"); TableHeader(h.Cell(), "Tên phòng ban"); TableHeader(h.Cell(), "Tổng đơn/thư");
                                TableHeader(h.Cell(), "Hoàn thành"); TableHeader(h.Cell(), "Từ chối"); TableHeader(h.Cell(), "Feedback");
                            });
                            var i = 0;
                            foreach (var r in dep.Rows)
                            {
                                Cell(table.Cell(), (++i).ToString(Vi));
                                Cell(table.Cell(), r.Name);
                                Cell(table.Cell(), r.TotalRequests.ToString(Vi));
                                Cell(table.Cell(), r.Completed.ToString(Vi));
                                Cell(table.Cell(), r.Rejected.ToString(Vi));
                                Cell(table.Cell(), Fb(r.FeedbackAverage, r.FeedbackCount));
                            }
                        });
                    }

                    if (sections.Contains("EXPENSES"))
                    {
                        var ex = d.Expenses;
                        col.Item().Text("5 · THỐNG KÊ CHI PHÍ").Bold().FontSize(12).FontColor(BrandBlue);
                        col.Item().Text($"Tổng chi phí: {ex.TotalAmount:N0} ₫ · Host: {ex.TotalGeneral:N0} ₫ · Hậu cần PB: {ex.TotalLogistics:N0} ₫")
                            .FontSize(9.5f);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28); c.RelativeColumn(3); c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); c.RelativeColumn(1.2f);
                            });
                            table.Header(h =>
                            {
                                TableHeader(h.Cell(), "STT"); TableHeader(h.Cell(), "Tên đoàn khách"); TableHeader(h.Cell(), "Thời gian");
                                TableHeader(h.Cell(), "Chi phí Host"); TableHeader(h.Cell(), "Chi phí Hậu cần"); TableHeader(h.Cell(), "Tổng chi phí"); TableHeader(h.Cell(), "Trạng thái");
                            });
                            var i = 0;
                            foreach (var r in ex.Rows)
                            {
                                Cell(table.Cell(), (++i).ToString(Vi));
                                Cell(table.Cell(), r.DelegationName);
                                Cell(table.Cell(), r.VisitDate?.ToString("dd/MM/yyyy") ?? "—");
                                Cell(table.Cell(), $"{r.GeneralExpense:N0} ₫");
                                Cell(table.Cell(), $"{r.LogisticsExpense:N0} ₫");
                                Cell(table.Cell(), $"{r.TotalExpense:N0} ₫");
                                Cell(table.Cell(), r.Status);
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

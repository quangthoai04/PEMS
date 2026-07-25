using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

/// <summary>Draws the "MEETING AGENDA" A4 PDF from an already-resolved <see cref="ScheduleReportDto"/>,
/// styled after the reference "Meeting Agenda - Asia University and FPT University" template: two
/// logos in the header (repeats every page), a centered title block (page 1 only, since it lives in
/// the flowing content — not the repeating header), bullet-style participant lists, and a
/// peach-header / zebra-striped agenda table. No DB/file access here — pure layout, so the
/// data-mapping rules stay unit-testable separately.</summary>
public static class ScheduleReportPdfRenderer
{
    private static readonly string TableHeaderBg = "#F4DEC4";
    private static readonly string ZebraBg = "#EFEFEF";
    private static readonly string BorderColor = "#999999";
    private static readonly string MutedText = Colors.Grey.Darken1;

    public static byte[] Render(ScheduleReportDto dto, byte[] fptLogoBytes, byte[]? partnerLogoBytes, string languageCode = "vi")
    {
        bool hasPartnerLogo = partnerLogoBytes is { Length: > 0 };
        bool isEnglish = string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase);
        string L(string vi, string en) => isEnglish ? en : vi;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor(Colors.Black));

                // Logos repeat on every page; the title block below lives in Content (flows once,
                // at the top of page 1 only) — same visual result as the reference template.
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        if (hasPartnerLogo)
                        {
                            row.ConstantItem(100).Height(55).Image(fptLogoBytes).FitArea();
                            row.RelativeItem();
                            row.ConstantItem(90).Height(55).AlignRight().Image(partnerLogoBytes!).FitArea();
                        }
                        else
                        {
                            row.RelativeItem().AlignCenter().Height(55).Width(130).Image(fptLogoBytes).FitArea();
                        }
                    });
                    header.Item().PaddingTop(6).LineHorizontal(0.75f).LineColor(BorderColor);
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(12);

                    // ── Title block (page 1 only — plain flowing content, not a repeating header) ──
                    col.Item().Column(title =>
                    {
                        title.Item().AlignCenter().Text("MEETING AGENDA").Bold().FontSize(17);
                        title.Item().AlignCenter().Text(dto.DelegationName).Italic().FontSize(12);
                    });

                    // ── Overview fields ──
                    col.Item().Column(info =>
                    {
                        info.Spacing(4);
                        FieldLine(info, L("Thời gian", "Time"), FormatTimeRange(dto.PlannedStartAt, dto.PlannedEndAt));
                        FieldLine(info, L("Địa điểm", "Location"), dto.Location);
                        FieldLine(info, L("Mục tiêu", "Objective"), string.IsNullOrWhiteSpace(dto.Purpose) ? "-" : dto.Purpose);
                    });

                    // ── 1. Thành phần phía khách ──
                    col.Item().Column(section =>
                    {
                        section.Item().Text(L("1. Thành phần phía khách", "1. Guest Delegation")).Bold().FontSize(11).Underline();
                        section.Item().PaddingTop(4).Element(c => PersonBulletList(c, dto.GuestSide, isEnglish));
                    });

                    // ── 2. Thành phần phía FPT ──
                    col.Item().Column(section =>
                    {
                        section.Item().Text(L("2. Thành phần phía FPT", "2. FPT Delegation")).Bold().FontSize(11).Underline();
                        section.Item().PaddingTop(4).Element(c => PersonBulletList(c, dto.FptSide, isEnglish));
                    });

                    // ── 3. Lịch trình ──
                    col.Item().Column(section =>
                    {
                        section.Item().Text(L("3. Lịch trình", "3. Schedule")).Bold().FontSize(11).Underline();
                        section.Item().PaddingTop(4).Element(c => AgendaTable(c, dto.Agenda, isEnglish));
                    });
                });

                page.Footer().AlignCenter().PaddingTop(6).Text(x =>
                {
                    x.DefaultTextStyle(y => y.FontSize(9).FontColor(MutedText));
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void FieldLine(QuestPDF.Fluent.ColumnDescriptor col, string label, string value)
    {
        col.Item().Text(t =>
        {
            t.Span($"{label}: ").Bold();
            t.Span(value);
        });
    }

    private static void PersonBulletList(QuestPDF.Infrastructure.IContainer container, List<ScheduleReportPersonDto> people, bool isEnglish)
    {
        if (people.Count == 0)
        {
            container.Text(isEnglish ? "No information available" : "Chưa có thông tin").Italic().FontColor(MutedText);
            return;
        }

        container.Column(col =>
        {
            col.Spacing(3);
            foreach (var p in people)
            {
                col.Item().Row(row =>
                {
                    row.ConstantItem(14).Text("•");
                    row.RelativeItem().Text(t =>
                    {
                        t.Span(p.FullName).Bold();
                        if (!string.IsNullOrWhiteSpace(p.Organization))
                            t.Span($" — {p.Organization}");
                        if (!string.IsNullOrWhiteSpace(p.RoleLabel))
                        {
                            t.Span(", ");
                            t.Span(p.RoleLabel).Italic();
                        }
                    });
                });
            }
        });
    }

    private static void AgendaTable(QuestPDF.Infrastructure.IContainer container, List<ScheduleReportAgendaRowDto> rows, bool isEnglish)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.7f);
                columns.RelativeColumn(4f);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.8f);
            });

            table.Header(header =>
            {
                HeaderCell(header, "Time");
                HeaderCell(header, "Activity Description");
                HeaderCell(header, "Venue");
                HeaderCell(header, "Party in Charge");
            });

            for (var i = 0; i < rows.Count; i++)
            {
                var a = rows[i];
                string bg = i % 2 == 1 ? ZebraBg : Colors.White;

                GridCell(table, bg).Text(FormatTimeSpan(a.StartTime, a.EndTime)).FontSize(9).Bold();

                var descCell = GridCell(table, bg);
                descCell.Column(c =>
                {
                    c.Item().Text(a.Title).FontSize(9).SemiBold();
                    if (!string.IsNullOrWhiteSpace(a.Description))
                        c.Item().Text(a.Description).FontSize(8.5f).LineHeight(1.3f);
                });

                GridCell(table, bg).Text(a.Venue).FontSize(9);
                GridCell(table, bg).Text("FPT University").FontSize(9);
            }

            if (rows.Count == 0)
            {
                table.Cell().ColumnSpan(4).Border(0.75f).BorderColor(BorderColor).Padding(8)
                    .AlignCenter().Text(isEnglish ? "No schedule content has been set up yet" : "Chưa có nội dung lịch trình được thiết lập").Italic().FontColor(MutedText);
            }
        });
    }

    private static void HeaderCell(QuestPDF.Fluent.TableCellDescriptor header, string text)
    {
        header.Cell().Border(0.75f).BorderColor(BorderColor).Background(TableHeaderBg).Padding(6)
            .Text(text).FontColor(Colors.Black).Bold().FontSize(9);
    }

    private static QuestPDF.Infrastructure.IContainer GridCell(QuestPDF.Fluent.TableDescriptor table, string bg)
        => table.Cell().Border(0.75f).BorderColor(BorderColor).Background(bg).Padding(6);

    private static string FormatTimeRange(DateTime start, DateTime end)
    {
        var datePart = start.Date == end.Date
            ? start.ToString("dddd, MMMM dd, yyyy", CultureInfo.InvariantCulture)
            : $"{start.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture)} - {end.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture)}";
        return $"{datePart}, {FormatTimeSpan(start, end)} (GMT+7)";
    }

    private static string FormatTimeSpan(DateTime start, DateTime? end)
    {
        var s = start.ToString("h:mm tt", CultureInfo.InvariantCulture);
        return end.HasValue ? $"{s} - {end.Value.ToString("h:mm tt", CultureInfo.InvariantCulture)}" : s;
    }
}

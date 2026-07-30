using System;
using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Reports.Common;

/// <summary>
/// The document a report email carries. Before Batch 9 these numbers were pasted into the email body as
/// an HTML table; the body now comes from <c>email_templates</c> and says "đính kèm là báo cáo…", so the
/// figures live here instead — in the attachment the template promises.
///
/// <para>
/// The layout is not new. It reproduces the house style the download exports already use (A4, 1.6 cm
/// margin, blue title, blue-headed tables, page footer) so a report a recipient receives by mail and the
/// same report downloaded from the screen look like the same document.
/// </para>
/// </summary>
public sealed record ReportPdfModel(
    string Title,
    string Subtitle,
    IReadOnlyList<ReportPdfBlock> Blocks);

/// <summary>One part of a report document. Closed set — a caller cannot invent a new kind of block.</summary>
public abstract record ReportPdfBlock
{
    private ReportPdfBlock() { }

    /// <summary>The low-rating callout the HTML bodies used to draw in a red box.</summary>
    public sealed record Warning(string Text) : ReportPdfBlock;

    /// <summary>Label/value pairs — the "Tổng đoàn khách / Feedback trung bình / …" summary.</summary>
    public sealed record Metrics(IReadOnlyList<ReportPdfMetric> Rows) : ReportPdfBlock;

    /// <summary>A listing. <paramref name="EmptyText"/> is shown instead of an empty grid.</summary>
    public sealed record Table(
        string Title,
        IReadOnlyList<ReportPdfColumn> Columns,
        IReadOnlyList<IReadOnlyList<string>> Rows,
        string EmptyText) : ReportPdfBlock;

    /// <summary>A free line of prose, e.g. the sender's closing note.</summary>
    public sealed record Note(string Label, string Text) : ReportPdfBlock;
}

public sealed record ReportPdfMetric(string Label, string Value);

/// <param name="Width">Relative width; <see cref="Fixed"/> columns use an absolute point width instead.</param>
public sealed record ReportPdfColumn(string Header, float Width, bool AlignRight = false, bool Fixed = false);

/// <summary>Renders <see cref="ReportPdfModel"/> to PDF bytes.</summary>
public static class ReportPdf
{
    private const string BrandBlue = "#004C91";
    private const string HeaderFill = "#F8FAFC";
    private const string WarningFill = "#FEF2F2";
    private const string WarningText = "#7F1D1D";

    static ReportPdf() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Render(ReportPdfModel model)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(model.Title).Bold().FontSize(16).FontColor(BrandBlue);
                    col.Item().Text(model.Subtitle).FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(14);
                    foreach (var block in model.Blocks) RenderBlock(col, block);
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

        return document.GeneratePdf();
    }

    private static void RenderBlock(ColumnDescriptor col, ReportPdfBlock block)
    {
        switch (block)
        {
            case ReportPdfBlock.Warning warning:
                col.Item()
                    .Background(WarningFill)
                    .Border(0.5f).BorderColor("#FECACA")
                    .Padding(10)
                    .Text(warning.Text).FontSize(9.5f).FontColor(WarningText);
                break;

            case ReportPdfBlock.Metrics metrics:
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(6); });
                    foreach (var row in metrics.Rows)
                    {
                        table.Cell().Background(HeaderFill).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(4).Text(row.Label).Bold().FontSize(9);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(4).Text(row.Value).FontSize(9);
                    }
                });
                break;

            case ReportPdfBlock.Table listing:
                col.Item().Text($"{listing.Title} ({listing.Rows.Count})")
                    .Bold().FontSize(12).FontColor(BrandBlue);
                if (listing.Rows.Count == 0)
                {
                    col.Item().Text(listing.EmptyText).FontSize(9).FontColor(Colors.Grey.Darken1);
                    break;
                }
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        foreach (var column in listing.Columns)
                        {
                            if (column.Fixed) c.ConstantColumn(column.Width);
                            else c.RelativeColumn(column.Width);
                        }
                    });
                    table.Header(h =>
                    {
                        foreach (var column in listing.Columns)
                        {
                            var cell = h.Cell().Background(BrandBlue).Padding(4);
                            var text = column.AlignRight ? cell.AlignRight() : cell;
                            text.Text(column.Header).Bold().FontSize(9).FontColor(Colors.White);
                        }
                    });
                    foreach (var row in listing.Rows)
                    {
                        for (var i = 0; i < listing.Columns.Count; i++)
                        {
                            var cell = table.Cell()
                                .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(4);
                            var text = listing.Columns[i].AlignRight ? cell.AlignRight() : cell;
                            text.Text(i < row.Count ? row[i] : string.Empty).FontSize(9);
                        }
                    }
                });
                break;

            case ReportPdfBlock.Note note:
                col.Item().Text(t =>
                {
                    t.Span($"{note.Label}: ").Bold().FontSize(9.5f);
                    t.Span(note.Text).FontSize(9.5f);
                });
                break;
        }
    }
}

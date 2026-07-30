using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PEMS.Application.Reports.Common;

/// <summary>One priced line of a logistics invoice.</summary>
public sealed record InvoiceLine(
    string Title,
    string Delegation,
    DateTime StartAt,
    int Quantity,
    decimal UnitPrice,
    decimal Amount);

/// <summary>Everything the invoice document shows, independent of who is sending it to whom.</summary>
public sealed record InvoiceDocument(
    string Title,
    string DepartmentName,
    string CampusName,
    string PeriodFrom,
    string PeriodTo,
    DateTime IssuedAt,
    IReadOnlyList<InvoiceLine> Lines,
    decimal GrandTotal,
    string? Note,
    string SenderLabel);

/// <summary>
/// The logistics invoice, laid out once for both directions it travels: the campus billing a department
/// (C-26) and a department billing the campus (C-29).
///
/// <para>
/// Sharing the layout is the point. The two used to be near-identical copies of the same HTML builder,
/// which is how a change to one silently stopped matching the other — and both are now rendered from the
/// same <c>REPORT_DEPARTMENT_INVOICE</c> template, so a recipient reading one and then the other should
/// not be able to tell which handler produced it.
/// </para>
/// </summary>
public static class InvoicePdf
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Money(decimal value) => value.ToString("#,##0", Vi) + " ₫";

    public static ReportPdfModel Build(InvoiceDocument invoice)
    {
        var ordered = invoice.Lines.OrderBy(l => l.StartAt).ToList();

        var rows = ordered
            .Select((l, i) => (IReadOnlyList<string>)new[]
            {
                (i + 1).ToString(Vi),
                l.Title,
                l.Delegation,
                l.StartAt.ToString("dd/MM/yyyy"),
                l.Quantity.ToString(Vi),
                Money(l.UnitPrice),
                Money(l.Amount),
            })
            .ToList();

        var blocks = new List<ReportPdfBlock>
        {
            new ReportPdfBlock.Table(
                "Bảng kê hạng mục hậu cần",
                new[]
                {
                    new ReportPdfColumn("STT", 28, Fixed: true),
                    new ReportPdfColumn("Hạng mục", 2.6f),
                    new ReportPdfColumn("Đoàn khách", 2.2f),
                    new ReportPdfColumn("Ngày", 1.3f),
                    new ReportPdfColumn("SL", 0.7f, AlignRight: true),
                    new ReportPdfColumn("Đơn giá", 1.4f, AlignRight: true),
                    new ReportPdfColumn("Thành tiền", 1.5f, AlignRight: true),
                },
                rows,
                "Không có hạng mục nào trong hóa đơn."),

            new ReportPdfBlock.Metrics(new[]
            {
                new ReportPdfMetric("TỔNG CỘNG", Money(invoice.GrandTotal)),
            }),
        };

        if (!string.IsNullOrWhiteSpace(invoice.Note))
            blocks.Add(new ReportPdfBlock.Note("Ghi chú", invoice.Note!.Trim()));

        blocks.Add(new ReportPdfBlock.Note("Người gửi", invoice.SenderLabel));

        return new ReportPdfModel(
            invoice.Title,
            $"{invoice.DepartmentName} · {invoice.CampusName} · Kỳ {invoice.PeriodFrom} – {invoice.PeriodTo} "
            + $"· Lập lúc {invoice.IssuedAt:HH:mm dd/MM/yyyy}",
            blocks);
    }
}

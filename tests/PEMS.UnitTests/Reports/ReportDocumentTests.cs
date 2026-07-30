using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Reports.Common;

namespace PEMS.UnitTests.Reports;

/// <summary>
/// The period labels, the scope wording and the generated document — the three things the email body and
/// its attachment must agree about.
/// </summary>
public class ReportDocumentTests
{
    // ── Period ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The reporting queries filter on a half-open range. A reader expects the last day INSIDE the
    /// period, so the label steps back one day — the conversion that must happen exactly once.
    /// </summary>
    [Fact]
    public void The_period_label_names_the_last_day_inside_the_range()
    {
        var (from, to) = ReportPeriod.Labels(new DateTime(2026, 7, 1), new DateTime(2026, 8, 1));

        Assert.Equal("01/07/2026", from);
        Assert.Equal("31/07/2026", to);
    }

    [Fact]
    public void A_single_day_period_reads_as_that_day_twice()
    {
        var (from, to) = ReportPeriod.Labels(new DateTime(2026, 7, 27), new DateTime(2026, 7, 28));

        Assert.Equal("27/07/2026", from);
        Assert.Equal(from, to);
    }

    /// <summary>
    /// The invoice panels do not filter by period — the lines are the ones the sender ticked. An absent
    /// start therefore stays absent instead of being invented as "the start of the year".
    /// </summary>
    [Fact]
    public void An_invoice_without_a_start_date_says_so_rather_than_inventing_one()
    {
        var (from, to) = ReportPeriod.InvoiceLabels(null, new DateTime(2026, 7, 20), new DateTime(2026, 7, 27));

        Assert.Equal(ReportPeriod.NotSpecified, from);
        Assert.Equal("20/07/2026", to);
    }

    [Fact]
    public void An_invoice_without_an_end_date_uses_the_day_it_was_issued()
    {
        var (_, to) = ReportPeriod.InvoiceLabels(new DateTime(2026, 7, 1), null, new DateTime(2026, 7, 27));

        Assert.Equal("27/07/2026", to);
    }

    // ── Scope wording ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(PersonnelReportScope.VisitSupport, "tham gia tiếp khách", "visit support")]
    [InlineData(PersonnelReportScope.DelegationHosting, "phụ trách đoàn khách", "delegation hosting")]
    [InlineData(PersonnelReportScope.VisitAssignments, "nhiệm vụ tiếp khách", "visit assignments")]
    public void Each_scope_has_one_phrase_per_language(PersonnelReportScope scope, string vi, string en)
    {
        Assert.Equal(vi, PersonnelReportScopes.Label(scope, EmailLanguages.Vi));
        Assert.Equal(en, PersonnelReportScopes.Label(scope, EmailLanguages.En));
    }

    [Fact]
    public void An_unknown_language_falls_back_to_vietnamese()
        => Assert.Equal("tham gia tiếp khách",
            PersonnelReportScopes.Label(PersonnelReportScope.VisitSupport, "fr"));

    // ── The document itself ─────────────────────────────────────────────────

    [Fact]
    public void A_rendered_report_is_a_real_pdf()
    {
        var bytes = ReportPdf.Render(new ReportPdfModel(
            "BÁO CÁO VẬN HÀNH CAMPUS",
            "FPTU Hà Nội · Kỳ 01/07/2026 – 31/07/2026",
            new ReportPdfBlock[]
            {
                new ReportPdfBlock.Warning("Cảnh báo chất lượng: 1,5★ (dưới 2★)."),
                new ReportPdfBlock.Metrics(new[] { new ReportPdfMetric("Tổng đoàn khách", "12") }),
                new ReportPdfBlock.Note("Người gửi", "Head Office"),
            }));

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void A_report_with_an_empty_listing_still_renders()
    {
        var bytes = ReportPdf.Render(new ReportPdfModel(
            "BÁO CÁO PHỐI HỢP TIẾP KHÁCH CỦA PHÒNG BAN",
            "Phòng Hành chính · Kỳ 01/07/2026 – 31/07/2026",
            new ReportPdfBlock[]
            {
                new ReportPdfBlock.Table(
                    "Danh sách nhiệm vụ phòng ban đã nhận trong kỳ",
                    new[] { new ReportPdfColumn("STT", 28, Fixed: true), new ReportPdfColumn("Loại", 2) },
                    Array.Empty<IReadOnlyList<string>>(),
                    "Phòng ban không nhận đơn yêu cầu/thư mời nào trong kỳ báo cáo."),
            }));

        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    // ── Invoice ─────────────────────────────────────────────────────────────

    [Fact]
    public void An_invoice_totals_quantity_times_unit_price_per_line()
    {
        var lines = new[]
        {
            new InvoiceLine("Thuê màn LED", "Đoàn Nhật Bản", new DateTime(2026, 7, 10), 2, 1_500_000m, 3_000_000m),
            new InvoiceLine("Nước uống", "Đoàn Nhật Bản", new DateTime(2026, 7, 11), 30, 12_000m, 360_000m),
        };

        var model = InvoicePdf.Build(new InvoiceDocument(
            "HÓA ĐƠN HẬU CẦN TIẾP KHÁCH", "Phòng Hành chính", "FPTU Hà Nội",
            "01/07/2026", "31/07/2026", new DateTime(2026, 7, 27, 9, 0, 0),
            lines, lines.Sum(l => l.Amount), Note: null, SenderLabel: "Staff Leader"));

        var total = model.Blocks.OfType<ReportPdfBlock.Metrics>().Single().Rows.Single();
        Assert.Equal("TỔNG CỘNG", total.Label);
        Assert.Equal(InvoicePdf.Money(3_360_000m), total.Value);
    }

    /// <summary>Both invoice directions render from one layout, so neither can drift from the other.</summary>
    [Fact]
    public void Both_invoice_directions_share_the_same_columns()
    {
        ReportPdfModel Build(string title) => InvoicePdf.Build(new InvoiceDocument(
            title, "Phòng Hành chính", "FPTU Hà Nội", "01/07/2026", "31/07/2026",
            new DateTime(2026, 7, 27), Array.Empty<InvoiceLine>(), 0m, null, "Người gửi"));

        var downwards = Build("HÓA ĐƠN HẬU CẦN TIẾP KHÁCH");
        var upwards = Build("HÓA ĐƠN HẬU CẦN TIẾP KHÁCH (ĐÃ HOÀN THÀNH)");

        Assert.Equal(
            downwards.Blocks.OfType<ReportPdfBlock.Table>().Single().Columns.Select(c => c.Header),
            upwards.Blocks.OfType<ReportPdfBlock.Table>().Single().Columns.Select(c => c.Header));
    }

    [Fact]
    public void An_invoice_renders_to_a_real_pdf()
    {
        var bytes = ReportPdf.Render(InvoicePdf.Build(new InvoiceDocument(
            "HÓA ĐƠN HẬU CẦN TIẾP KHÁCH", "Phòng Hành chính", "FPTU Hà Nội",
            ReportPeriod.NotSpecified, "27/07/2026", new DateTime(2026, 7, 27),
            new[] { new InvoiceLine("Thuê màn LED", "Đoàn Nhật Bản", new DateTime(2026, 7, 10), 1, 100m, 100m) },
            100m, "Ghi chú thử", "Staff Leader · FPTU Hà Nội")));

        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }
}

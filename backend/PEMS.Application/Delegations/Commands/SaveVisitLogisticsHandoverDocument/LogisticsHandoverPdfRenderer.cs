using System.Text.Json;
using PEMS.Application.Common;
using PEMS.Domain.Constants;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Delegations.Commands.SaveVisitLogisticsHandoverDocument;

/// <summary>
/// Server-side PDF of a fully-signed logistics handover record — same content shape as
/// <c>TaskHandoverModal.tsx</c> (header/parties, checklist table, signature status), built the same
/// way <c>ExportMinutesPdfQueryHandler</c> builds the meeting-minutes PDF (QuestPDF, same style
/// constants/table helpers).
/// </summary>
public static class LogisticsHandoverPdfRenderer
{
    static LogisticsHandoverPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly string PrimaryColor = Colors.Orange.Darken2;
    private static readonly string BorderColor = Colors.Grey.Lighten1;
    private static readonly string HeaderBg = Colors.Orange.Darken2;
    private static readonly string ZebraBg = Colors.Grey.Lighten4;
    private static readonly string MutedText = Colors.Grey.Darken1;

    public sealed record ChecklistRow(string Name, string Qty, string Giao, string Nhan);

    public sealed record SignatureInfo(string? Name, DateTime? SignedAt);

    public sealed record Input(
        ulong LogisticsItemId,
        string HandoverType,
        string ItemTitle,
        string? ItemDescription,
        int Quantity,
        string DelegationName,
        string CampusName,
        SignatureInfo ProviderSignature,
        SignatureInfo BorrowerSignature,
        string? ItemCondition,
        string? Note,
        IReadOnlyList<ChecklistRow> Checklist);

    public static byte[] Build(Input input)
    {
        var title = input.HandoverType == LogisticsHandoverTypes.Return
            ? "BIÊN BẢN NGHIỆM THU (TRẢ)"
            : "BIÊN BẢN BÀN GIAO (MƯỢN)";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.8f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor(Colors.Black));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(title).Bold().FontSize(16).FontColor(PrimaryColor);
                            c.Item().Text("Partnership Engagement Management System").FontSize(8).FontColor(MutedText);
                        });
                        row.ConstantItem(160).AlignRight().Column(c =>
                        {
                            c.Item().Text($"Mã hạng mục: LG-{input.LogisticsItemId}").FontSize(9).FontColor(MutedText);
                            c.Item().Text($"Ngày xuất: {VietnamTime.Now():dd/MM/yyyy HH:mm}").FontSize(9).FontColor(MutedText);
                        });
                    });
                    header.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(PrimaryColor);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Text(input.ItemTitle).Bold().FontSize(14);

                    col.Item().Border(1).BorderColor(BorderColor).Padding(10).Column(info =>
                    {
                        info.Spacing(5);
                        InfoRow(info, "Đoàn khách", input.DelegationName);
                        InfoRow(info, "Campus", input.CampusName);
                        InfoRow(info, "Số lượng", input.Quantity.ToString());
                        if (!string.IsNullOrWhiteSpace(input.ItemDescription))
                            InfoRow(info, "Mô tả", input.ItemDescription!);
                        if (!string.IsNullOrWhiteSpace(input.ItemCondition))
                            InfoRow(info, "Tình trạng tài sản", ConditionLabel(input.ItemCondition!));
                    });

                    if (input.Checklist.Count > 0)
                    {
                        col.Item().Column(section =>
                        {
                            section.Item().Text("DANH MỤC BÀN GIAO").Bold().FontSize(11).FontColor(PrimaryColor);
                            section.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(28);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(2.2f);
                                    columns.RelativeColumn(2.2f);
                                });
                                table.Header(header =>
                                {
                                    HeaderCell(header, "#");
                                    HeaderCell(header, "Nội dung");
                                    HeaderCell(header, "SL");
                                    HeaderCell(header, "Tình trạng bàn giao");
                                    HeaderCell(header, "Tình trạng nghiệm thu");
                                });
                                for (var i = 0; i < input.Checklist.Count; i++)
                                {
                                    var row = input.Checklist[i];
                                    string bg = i % 2 == 1 ? ZebraBg : Colors.White;
                                    BodyCell(table, (i + 1).ToString(), bg);
                                    BodyCell(table, row.Name, bg);
                                    BodyCell(table, row.Qty, bg);
                                    BodyCell(table, row.Giao, bg);
                                    BodyCell(table, row.Nhan, bg);
                                }
                            });
                        });
                    }

                    col.Item().Column(section =>
                    {
                        section.Item().Text("XÁC NHẬN KÝ").Bold().FontSize(11).FontColor(PrimaryColor);
                        section.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(BorderColor).Padding(10).Column(c =>
                            {
                                c.Item().Text("Bên giao").Bold().FontSize(10);
                                c.Item().Text(input.ProviderSignature.Name ?? "Chưa ký").FontSize(10);
                                if (input.ProviderSignature.SignedAt is { } signedAt)
                                    c.Item().Text($"Ký lúc: {signedAt:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(MutedText);
                            });
                            row.ConstantItem(12);
                            row.RelativeItem().Border(1).BorderColor(BorderColor).Padding(10).Column(c =>
                            {
                                c.Item().Text("Bên nhận").Bold().FontSize(10);
                                c.Item().Text(input.BorrowerSignature.Name ?? "Chưa ký").FontSize(10);
                                if (input.BorrowerSignature.SignedAt is { } signedAt)
                                    c.Item().Text($"Ký lúc: {signedAt:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(MutedText);
                            });
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(input.Note))
                    {
                        col.Item().Column(section =>
                        {
                            section.Item().Text("GHI CHÚ").Bold().FontSize(11).FontColor(PrimaryColor);
                            section.Item().PaddingTop(4).Border(1).BorderColor(BorderColor).Padding(10)
                                .Text(input.Note!).FontSize(10).LineHeight(1.4f);
                        });
                    }
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(BorderColor);
                    footer.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Tài liệu được tạo tự động từ hệ thống PEMS.").FontSize(8).FontColor(MutedText);
                        row.ConstantItem(100).AlignRight().Text(x =>
                        {
                            x.DefaultTextStyle(y => y.FontSize(8).FontColor(MutedText));
                            x.Span("Trang ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Parses the checklist rows JSON array (<c>[{name,qty,giao,nhan}]</c>) — same shape the
    /// frontend's VehicleChecklistRow uses, extracted via VehicleHandoverChecklistNote.ExtractRowsJson.</summary>
    public static IReadOnlyList<ChecklistRow> ParseChecklist(string? rowsJson)
    {
        if (string.IsNullOrWhiteSpace(rowsJson)) return Array.Empty<ChecklistRow>();
        try
        {
            using var doc = JsonDocument.Parse(rowsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<ChecklistRow>();
            var rows = new List<ChecklistRow>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                rows.Add(new ChecklistRow(
                    Field(el, "name"), Field(el, "qty"), Field(el, "giao"), Field(el, "nhan")));
            }
            return rows;
        }
        catch (JsonException)
        {
            return Array.Empty<ChecklistRow>();
        }
    }

    private static string Field(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string ConditionLabel(string condition) => condition switch
    {
        HandoverItemConditions.Good => "Tốt",
        HandoverItemConditions.Damaged => "Hư hỏng",
        HandoverItemConditions.Missing => "Thiếu/mất",
        HandoverItemConditions.Other => "Khác",
        _ => condition,
    };

    private static void InfoRow(QuestPDF.Fluent.ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(140).Text(label).FontColor(MutedText).SemiBold();
            row.RelativeItem().Text(value).SemiBold();
        });
    }

    private static void HeaderCell(QuestPDF.Fluent.TableCellDescriptor header, string text)
    {
        header.Cell().Background(HeaderBg).Padding(6).Text(text).FontColor(Colors.White).Bold().FontSize(9);
    }

    private static void BodyCell(QuestPDF.Fluent.TableDescriptor table, string text, string bg)
    {
        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(6).Text(text).FontSize(9);
    }
}

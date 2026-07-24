using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Reports.Queries.GetDeptLeaderInvoiceData;
using PEMS.Application.Reports.Queries.GetDeptLeaderReportOverview;
using PEMS.Domain.Constants;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using PEMS.Application.Common;
namespace PEMS.Application.Reports.Commands.ExportDeptLeaderInvoice;

/// <summary>
/// Builds the "Hóa đơn chuẩn bị hậu cần" PDF with QuestPDF. Nothing is stored:
/// no invoice table exists yet, so the file is generated and streamed for download only.
/// Quantities are re-read from visit_logistics_items (client values are ignored);
/// unit prices come from the request body.
/// </summary>
public sealed class ExportDeptLeaderInvoiceCommandHandler
    : IRequestHandler<ExportDeptLeaderInvoiceCommand, ExportDeptLeaderInvoiceResult>
{
    private const string BrandBlue = "#004C91";
    private const string BrandOrange = "#F37021";

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitFormReadService _formReadService;

    public ExportDeptLeaderInvoiceCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ExportDeptLeaderInvoiceResult> Handle(ExportDeptLeaderInvoiceCommand request, CancellationToken cancellationToken)
    {
        var deptId = DeptLeaderInvoiceGuard.RequireDepartmentLeader(_currentUser);

        if (request.Items.Count == 0)
            throw new ValidationException("Chọn ít nhất một hạng mục để xuất hóa đơn.");
        if (request.Items.Any(i => i.UnitPrice < 0))
            throw new ValidationException("Đơn giá phải lớn hơn hoặc bằng 0.");

        // Visit must be in the leader's department scope.
        var visit = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(ci => ci.VisitInstanceId == request.VisitInstanceId
                         && ci.LogisticsItems.Any(li => li.RequestedToDepartmentId == deptId))
            .Select(ci => new
            {
                ci.VisitInstanceId,
                ci.VisitRequestId,
                ci.VisitRequest.RequestCode,
                ci.PlannedStartAt,
                ci.PlannedEndAt,
                ci.CurrentHostUserId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (visit == null)
            throw new NotFoundException("Không tìm thấy chuyến thăm trong phạm vi phòng ban của bạn.");

        // This invoice is for ONE campus instance → it stamps THAT instance's per-campus delegation name
        // into the PDF, never a sibling campus. A missing detail raises the standard 409, no fallback.
        var visitEntity = await _db.VisitRequests.AsNoTracking()
            .FirstAsync(v => v.VisitRequestId == visit.VisitRequestId, cancellationToken);
        var formContent = await _formReadService.ResolveCampusFormContentAsync(
            visitEntity, new[] { visit.VisitInstanceId }, cancellationToken);
        var delegationName = formContent[visit.VisitInstanceId].DelegationName;

        // Re-read the requested items from the DB — quantity always comes from the host request.
        var requestedIds = request.Items.Select(i => i.LogisticsItemId).Distinct().ToList();
        var dbItems = await _db.VisitLogisticsItems.AsNoTracking()
            .Where(li => li.VisitInstanceId == request.VisitInstanceId
                         && li.RequestedToDepartmentId == deptId
                         && requestedIds.Contains(li.LogisticsItemId))
            .Select(li => new { li.LogisticsItemId, li.Title, li.ItemType, li.Quantity })
            .ToListAsync(cancellationToken);

        var lines = new List<InvoiceLine>();
        foreach (var input in request.Items)
        {
            var dbItem = dbItems.FirstOrDefault(x => x.LogisticsItemId == input.LogisticsItemId)
                ?? throw new ValidationException($"Hạng mục #{input.LogisticsItemId} không thuộc chuyến thăm/phòng ban của bạn.");
            var quantity = dbItem.Quantity ?? 1;
            lines.Add(new InvoiceLine
            {
                ItemName = dbItem.Title,
                ItemTypeLabel = DeptLeaderReportLabels.ItemTypeLabelVi(dbItem.ItemType),
                Quantity = quantity,
                Unit = string.IsNullOrWhiteSpace(input.Unit) ? "—" : input.Unit!.Trim(),
                UnitPrice = input.UnitPrice,
                Amount = quantity * input.UnitPrice,
                Note = input.Note?.Trim() ?? string.Empty,
            });
        }
        var grandTotal = lines.Sum(l => l.Amount);

        // Header metadata.
        var deptInfo = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == deptId)
            .Select(d => new { d.Name, CampusName = d.Campus.Name })
            .FirstOrDefaultAsync(cancellationToken);
        var generatedByName = _currentUser.UserId != null
            ? await _db.Users.AsNoTracking()
                .Where(u => u.UserId == _currentUser.UserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var hostName = visit.CurrentHostUserId != null
            ? await _db.Users.AsNoTracking()
                .Where(u => u.UserId == visit.CurrentHostUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var stampVn = VietnamTime.Now();
        var meta = new InvoiceMeta
        {
            InvoiceCode = $"PEMS-INV-{stampVn:yyyyMMdd-HHmm}",
            Title = string.IsNullOrWhiteSpace(request.InvoiceTitle) ? "HÓA ĐƠN CHUẨN BỊ HẬU CẦN" : request.InvoiceTitle!.Trim().ToUpperInvariant(),
            Note = request.InvoiceNote?.Trim() ?? string.Empty,
            GeneratedAtVn = stampVn,
            GeneratedByName = generatedByName ?? "—",
            DepartmentName = deptInfo?.Name ?? $"Phòng ban #{deptId}",
            CampusName = deptInfo?.CampusName ?? "—",
            DelegationName = delegationName,
            RequestCode = visit.RequestCode,
            VisitDate = $"{visit.PlannedStartAt:dd/MM/yyyy} – {visit.PlannedEndAt:dd/MM/yyyy}",
            HostName = hostName ?? "—",
        };

        return new ExportDeptLeaderInvoiceResult
        {
            Content = BuildPdf(meta, lines, grandTotal),
            ContentType = "application/pdf",
            FileName = $"PEMS_Department_Invoice_{visit.RequestCode}_{stampVn:yyyyMMdd_HHmm}.pdf",
        };
    }

    private sealed class InvoiceLine
    {
        public string ItemName { get; set; } = string.Empty;
        public string ItemTypeLabel { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Unit { get; set; } = "—";
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    private sealed class InvoiceMeta
    {
        public string InvoiceCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime GeneratedAtVn { get; set; }
        public string GeneratedByName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string CampusName { get; set; } = string.Empty;
        public string DelegationName { get; set; } = string.Empty;
        public string RequestCode { get; set; } = string.Empty;
        public string VisitDate { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
    }

    private static string Vnd(decimal v) => string.Format(Vi, "{0:N0} đ", v);

    private static byte[] BuildPdf(InvoiceMeta meta, IReadOnlyList<InvoiceLine> lines, decimal grandTotal)
    {
        var border = Colors.Grey.Lighten2;
        var muted = Colors.Grey.Darken1;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.8f, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Arial").FontColor(Colors.Black));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("FPT University · PEMS").FontSize(10).Bold().FontColor(BrandBlue);
                            c.Item().Text("Partnership Engagement Management System").FontSize(8).FontColor(muted);
                        });
                        row.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text($"Mã hóa đơn tạm: {meta.InvoiceCode}").FontSize(8.5f).FontColor(muted);
                            c.Item().AlignRight().Text($"Ngày xuất: {meta.GeneratedAtVn:dd/MM/yyyy HH:mm} (GMT+7)").FontSize(8.5f).FontColor(muted);
                        });
                    });
                    header.Item().PaddingTop(10).AlignCenter().Text(meta.Title).FontSize(16).Bold().FontColor(BrandBlue);
                    header.Item().PaddingTop(6).LineHorizontal(1).LineColor(BrandBlue);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    // ── Party / visit info ──
                    col.Item().Row(row =>
                    {
                        void InfoCell(QuestPDF.Infrastructure.IContainer cell, string title, IEnumerable<(string Label, string Value)> rows)
                        {
                            cell.Border(0.5f).BorderColor(border).Padding(8).Column(c =>
                            {
                                c.Item().Text(title).FontSize(9).Bold().FontColor(BrandBlue);
                                c.Item().PaddingTop(4).Table(t =>
                                {
                                    t.ColumnsDefinition(cd => { cd.ConstantColumn(110); cd.RelativeColumn(); });
                                    foreach (var (label, value) in rows)
                                    {
                                        t.Cell().PaddingVertical(1.5f).Text(label).FontSize(8.5f).FontColor(muted);
                                        t.Cell().PaddingVertical(1.5f).Text(value).FontSize(8.5f).SemiBold();
                                    }
                                });
                            });
                        }

                        row.RelativeItem().Element(c => InfoCell(c, "BÊN LẬP HÓA ĐƠN", new[]
                        {
                            ("Người xuất", meta.GeneratedByName),
                            ("Phòng ban", meta.DepartmentName),
                            ("Campus", meta.CampusName),
                        }));
                        row.ConstantItem(10);
                        row.RelativeItem().Element(c => InfoCell(c, "THÔNG TIN CHUYẾN THĂM", new[]
                        {
                            ("Tên đoàn", meta.DelegationName),
                            ("Mã visit/request", meta.RequestCode),
                            ("Ngày thăm", meta.VisitDate),
                            ("Host yêu cầu", meta.HostName),
                            ("Phòng ban chuẩn bị", meta.DepartmentName),
                        }));
                    });

                    // ── Detail table ──
                    col.Item().PaddingTop(14).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(28);   // STT
                            c.RelativeColumn(3f);   // Hạng mục
                            c.RelativeColumn(1.6f); // Loại
                            c.ConstantColumn(46);   // Số lượng
                            c.ConstantColumn(46);   // Đơn vị
                            c.RelativeColumn(1.4f); // Đơn giá
                            c.RelativeColumn(1.5f); // Thành tiền
                            c.RelativeColumn(1.8f); // Ghi chú
                        });

                        table.Header(h =>
                        {
                            void Th(string text, bool right = false)
                            {
                                var cell = h.Cell().Background(BrandBlue).Padding(4);
                                var t = right ? cell.AlignRight() : cell;
                                t.Text(text).FontColor(Colors.White).FontSize(8.5f).Bold();
                            }
                            Th("STT");
                            Th("Hạng mục");
                            Th("Loại");
                            Th("SL", right: true);
                            Th("Đơn vị");
                            Th("Đơn giá", right: true);
                            Th("Thành tiền", right: true);
                            Th("Ghi chú");
                        });

                        for (var i = 0; i < lines.Count; i++)
                        {
                            var line = lines[i];
                            void Td(string text, bool right = false, bool bold = false)
                            {
                                var cell = table.Cell().BorderBottom(0.5f).BorderColor(border).Padding(4);
                                var t = right ? cell.AlignRight() : cell;
                                var span = t.Text(text).FontSize(8.5f);
                                if (bold) span.SemiBold();
                            }
                            Td((i + 1).ToString());
                            Td(line.ItemName, bold: true);
                            Td(line.ItemTypeLabel);
                            Td(line.Quantity.ToString("N0", Vi), right: true);
                            Td(line.Unit);
                            Td(Vnd(line.UnitPrice), right: true);
                            Td(Vnd(line.Amount), right: true, bold: true);
                            Td(string.IsNullOrEmpty(line.Note) ? "—" : line.Note);
                        }

                        // Grand total row.
                        table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten4).Padding(5)
                            .AlignRight().Text("TỔNG CỘNG").FontSize(9.5f).Bold();
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(5)
                            .AlignRight().Text(Vnd(grandTotal)).FontSize(10.5f).Bold().FontColor(BrandOrange);
                        table.Cell().Background(Colors.Grey.Lighten4);
                    });

                    // ── Note ──
                    if (!string.IsNullOrEmpty(meta.Note))
                    {
                        col.Item().PaddingTop(10).Border(0.5f).BorderColor(border).Padding(8).Column(c =>
                        {
                            c.Item().Text("Ghi chú").FontSize(8.5f).Bold().FontColor(BrandBlue);
                            c.Item().PaddingTop(2).Text(meta.Note).FontSize(8.5f);
                        });
                    }

                    // ── Signatures ──
                    col.Item().PaddingTop(26).Row(row =>
                    {
                        void SignBox(QuestPDF.Infrastructure.IContainer cell, string title, string? name = null)
                        {
                            cell.Column(c =>
                            {
                                c.Item().AlignCenter().Text(title).FontSize(9).Bold();
                                c.Item().AlignCenter().Text("(Ký, ghi rõ họ tên)").FontSize(7.5f).FontColor(muted);
                                c.Item().Height(52);
                                c.Item().AlignCenter().Text(name ?? " ").FontSize(9).SemiBold();
                            });
                        }
                        row.RelativeItem().Element(c => SignBox(c, "NGƯỜI LẬP", meta.GeneratedByName));
                        row.RelativeItem().Element(c => SignBox(c, "XÁC NHẬN PHÒNG BAN"));
                        row.RelativeItem().Element(c => SignBox(c, "XÁC NHẬN HOST / IC"));
                    });
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.5f).LineColor(border);
                    footer.Item().PaddingTop(4).AlignCenter()
                        .Text($"Generated by PEMS · {meta.GeneratedAtVn:dd/MM/yyyy HH:mm} (GMT+7) · {meta.InvoiceCode}")
                        .FontSize(7.5f).FontColor(muted);
                });
            });
        }).GeneratePdf();
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using PEMS.Application.Common;
namespace PEMS.Application.MeetingMinutes.Queries.ExportMinutes;

public class ExportMinutesPdfQueryHandler : IRequestHandler<ExportMinutesPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly Reports.Common.IReportArchiveService _reportArchive;

    private static readonly string PrimaryColor = Colors.Blue.Darken2;
    private static readonly string BorderColor = Colors.Grey.Lighten1;
    private static readonly string HeaderBg = Colors.Blue.Darken2;
    private static readonly string ZebraBg = Colors.Grey.Lighten4;
    private static readonly string MutedText = Colors.Grey.Darken1;

    public ExportMinutesPdfQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, Reports.Common.IReportArchiveService reportArchive)
    {
        _db = db;
        _currentUser = currentUser;
        _reportArchive = reportArchive;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> Handle(ExportMinutesPdfQuery request, CancellationToken cancellationToken)
    {
        var minute = await _db.Minutes
            .Include(m => m.Participants)
            .Include(m => m.ActionItems)
            .FirstOrDefaultAsync(m => m.MinutesId == request.MinutesId, cancellationToken)
            ?? throw new NotFoundException("Minute", request.MinutesId);

        var vrc = await _db.VisitRequestCampuses
            .Include(v => v.VisitRequest)
            .FirstOrDefaultAsync(v => v.VisitInstanceId == minute.VisitInstanceId, cancellationToken);

        var campusName = vrc != null ? await _db.Campuses.Where(c => c.CampusId == vrc.CampusId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken) : null;

        // Security check
        if (_currentUser.PrimaryCampusId != null && vrc?.CampusId != _currentUser.PrimaryCampusId)
        {
            throw new ForbiddenException("Không có quyền tải PDF của campus này");
        }

        // Per-campus v2: this export is INSTANCE-scoped → a MIXED request uses THIS instance's detail.
        var delegationName = (vrc is null
            ? null
            : (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
                .ForInstancesAsync(_db, new[] { vrc.VisitInstanceId }, cancellationToken))
                .GetValueOrDefault(vrc.VisitInstanceId)) ?? "N/A";

        var pdfData = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.8f, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor(Colors.Black));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("BIÊN BẢN CUỘC HỌP").Bold().FontSize(18).FontColor(PrimaryColor);
                            c.Item().Text("Partnership Engagement Management System").FontSize(8).FontColor(MutedText);
                        });
                        row.ConstantItem(140).AlignRight().Column(c =>
                        {
                            c.Item().Text($"Mã biên bản: MIN-{minute.MinutesId}").FontSize(9).FontColor(MutedText);
                            c.Item().Text($"Ngày xuất: {VietnamTime.Now():dd/MM/yyyy HH:mm}").FontSize(9).FontColor(MutedText);
                        });
                    });
                    header.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(PrimaryColor);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Text(minute.Title).Bold().FontSize(14);

                    // Thông tin chung — bảng 2 cột không viền
                    col.Item().Border(1).BorderColor(BorderColor).Padding(10).Column(info =>
                    {
                        info.Spacing(5);
                        InfoRow(info, "Đoàn khách", delegationName);
                        InfoRow(info, "Campus", campusName ?? "N/A");
                        InfoRow(info, "Trạng thái biên bản", minute.Status);
                        InfoRow(info, "Ngày tạo", minute.CreatedAt.ToString("dd/MM/yyyy HH:mm"));
                        if (minute.UpdatedAt.HasValue)
                        {
                            InfoRow(info, "Cập nhật lần cuối", minute.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm"));
                        }
                    });

                    // Nội dung cuộc họp
                    col.Item().Column(section =>
                    {
                        section.Item().Text("NỘI DUNG CUỘC HỌP").Bold().FontSize(11).FontColor(PrimaryColor);
                        section.Item().PaddingTop(4).Border(1).BorderColor(BorderColor).Padding(10)
                            .Text(string.IsNullOrWhiteSpace(minute.Content) ? "Chưa có nội dung." : minute.Content)
                            .FontSize(10).LineHeight(1.4f);
                    });

                    // Người tham gia
                    col.Item().Column(section =>
                    {
                        var participants = minute.Participants.OrderBy(p => p.DisplayOrder).ToList();
                        section.Item().Text($"NGƯỜI THAM GIA & ĐIỂM DANH ({participants.Count})").Bold().FontSize(11).FontColor(PrimaryColor);

                        section.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(2.5f);
                            });

                            table.Header(header =>
                            {
                                HeaderCell(header, "Họ tên");
                                HeaderCell(header, "Vai trò");
                                HeaderCell(header, "Đơn vị");
                                HeaderCell(header, "Điểm danh");
                                HeaderCell(header, "Ghi chú");
                            });

                            for (var i = 0; i < participants.Count; i++)
                            {
                                var p = participants[i];
                                string bg = i % 2 == 1 ? ZebraBg : Colors.White;
                                BodyCell(table, p.FullNameSnapshot ?? "-", bg);
                                BodyCell(table, p.RoleSnapshot ?? "-", bg);
                                BodyCell(table, p.OrganizationSnapshot ?? "-", bg);
                                BodyCell(table, AttendanceLabel(p.AttendanceStatus), bg, AttendanceColor(p.AttendanceStatus));
                                BodyCell(table, p.AttendanceNote ?? "-", bg);
                            }

                            if (participants.Count == 0)
                            {
                                table.Cell().ColumnSpan(5).Padding(6).AlignCenter().Text("Chưa có người tham gia").FontColor(MutedText);
                            }
                        });
                    });

                    // Đầu mục công việc
                    col.Item().Column(section =>
                    {
                        var actionItems = minute.ActionItems.OrderBy(ai => ai.DisplayOrder).ToList();
                        section.Item().Text($"ĐẦU MỤC CÔNG VIỆC ({actionItems.Count})").Bold().FontSize(11).FontColor(PrimaryColor);

                        section.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                HeaderCell(header, "Công việc");
                                HeaderCell(header, "Ghi chú");
                                HeaderCell(header, "Trạng thái");
                                HeaderCell(header, "Deadline");
                                HeaderCell(header, "Hoàn thành");
                            });

                            for (var i = 0; i < actionItems.Count; i++)
                            {
                                var ai = actionItems[i];
                                string bg = i % 2 == 1 ? ZebraBg : Colors.White;
                                BodyCell(table, ai.Title ?? "-", bg);
                                BodyCell(table, ai.Note ?? "-", bg);
                                BodyCell(table, ActionStatusLabel(ai.Status), bg, ActionStatusColor(ai.Status));
                                BodyCell(table, ai.DueDate?.ToString("dd/MM/yyyy") ?? "-", bg);
                                BodyCell(table, ai.CompletedAt?.ToString("dd/MM/yyyy") ?? "-", bg);
                            }

                            if (actionItems.Count == 0)
                            {
                                table.Cell().ColumnSpan(5).Padding(6).AlignCenter().Text("Chưa có đầu mục công việc").FontColor(MutedText);
                            }
                        });
                    });
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

        if (_currentUser.UserId is { } userId)
        {
            var fileName = $"PEMS_BienBanCuocHop_MIN{minute.MinutesId}_{VietnamTime.Now():yyyyMMdd_HHmm}.pdf";
            await _reportArchive.ArchiveAsync(
                pdfData, fileName, "application/pdf", "MEETING_MINUTES", vrc?.CampusId, userId, cancellationToken);
        }

        return pdfData;
    }

    private static void InfoRow(QuestPDF.Fluent.ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(130).Text(label).FontColor(MutedText).SemiBold();
            row.RelativeItem().Text(value).SemiBold();
        });
    }

    private static void HeaderCell(QuestPDF.Fluent.TableCellDescriptor header, string text)
    {
        header.Cell().Background(HeaderBg).Padding(6).Text(text).FontColor(Colors.White).Bold().FontSize(9);
    }

    private static void BodyCell(QuestPDF.Fluent.TableDescriptor table, string text, string bg, string? textColor = null)
    {
        var cell = table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(BorderColor).Padding(6);
        var t = cell.Text(text).FontSize(9);
        if (textColor != null) t.FontColor(textColor).SemiBold();
    }

    private static string AttendanceLabel(string? status) => status switch
    {
        "PRESENT" => "Có mặt",
        "ABSENT" => "Vắng mặt",
        "EXCUSED" => "Vắng có phép",
        _ => status ?? "-"
    };

    private static string AttendanceColor(string? status) => status switch
    {
        "PRESENT" => Colors.Green.Darken2,
        "ABSENT" => Colors.Red.Darken1,
        "EXCUSED" => Colors.Orange.Darken2,
        _ => Colors.Black
    };

    private static string ActionStatusLabel(string? status) => status switch
    {
        "TODO" => "Cần làm",
        "IN_PROGRESS" => "Đang thực hiện",
        "DONE" => "Hoàn thành",
        "CANCELLED" => "Đã hủy",
        _ => status ?? "-"
    };

    private static string ActionStatusColor(string? status) => status switch
    {
        "TODO" => Colors.Orange.Darken2,
        "IN_PROGRESS" => Colors.Blue.Darken2,
        "DONE" => Colors.Green.Darken2,
        "CANCELLED" => Colors.Grey.Darken1,
        _ => Colors.Black
    };
}

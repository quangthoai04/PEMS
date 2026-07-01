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

namespace PEMS.Application.MeetingMinutes.Queries.ExportMinutes;

public class ExportMinutesPdfQueryHandler : IRequestHandler<ExportMinutesPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ExportMinutesPdfQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
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

        var pdfData = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Header().Text("BIÊN BẢN CUỘC HỌP").SemiBold().FontSize(18).FontColor(Colors.Blue.Darken2);

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    
                    col.Item().Text($"Tiêu đề: {minute.Title}").Bold().FontSize(14);
                    col.Item().Text($"Đoàn khách: {vrc?.VisitRequest?.DelegationName ?? "N/A"}");
                    col.Item().Text($"Campus: {campusName ?? "N/A"}");
                    col.Item().Text($"Trạng thái: {minute.Status}");
                    col.Item().Text($"Ngày tạo: {minute.CreatedAt:dd/MM/yyyy HH:mm}");
                    
                    col.Item().PaddingTop(10).Text("Nội dung cuộc họp:").Bold().FontSize(12);
                    col.Item().Text(minute.Content ?? "Chưa có nội dung.");

                    col.Item().PaddingTop(10).Text("Người tham gia:").Bold().FontSize(12);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Họ tên").Bold();
                            header.Cell().Text("Đơn vị").Bold();
                            header.Cell().Text("Điểm danh").Bold();
                        });

                        foreach (var p in minute.Participants.OrderBy(p => p.DisplayOrder))
                        {
                            table.Cell().Text(p.FullNameSnapshot ?? "-");
                            table.Cell().Text(p.OrganizationSnapshot ?? "-");
                            table.Cell().Text(p.AttendanceStatus ?? "-");
                        }
                    });

                    col.Item().PaddingTop(10).Text("Đầu mục công việc:").Bold().FontSize(12);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Công việc").Bold();
                            header.Cell().Text("Trạng thái").Bold();
                            header.Cell().Text("Deadline").Bold();
                        });

                        foreach (var ai in minute.ActionItems.OrderBy(ai => ai.DisplayOrder))
                        {
                            table.Cell().Text(ai.Title ?? "-");
                            table.Cell().Text(ai.Status ?? "-");
                            table.Cell().Text(ai.DueDate?.ToString("dd/MM/yyyy") ?? "-");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Trang ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();

        return pdfData;
    }
}

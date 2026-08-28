using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Minutes;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.MeetingMinutes.Queries.ExportMinutes;

public class ExportMinutesExcelQueryHandler : IRequestHandler<ExportMinutesExcelQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ExportMinutesExcelQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<byte[]> Handle(ExportMinutesExcelQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var minute = await _db.Minutes
            .Include(m => m.Participants)
            .Include(m => m.ActionItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.MinutesId == request.MinutesId, cancellationToken)
            ?? throw new NotFoundException("Minute", request.MinutesId);

        var vrc = await _db.VisitRequestCampuses
            .Include(v => v.VisitRequest)
            .FirstOrDefaultAsync(v => v.VisitInstanceId == minute.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", minute.VisitInstanceId);

        var campusName = await _db.Campuses.Where(c => c.CampusId == vrc.CampusId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken);

        // Per-campus v2: this export is INSTANCE-scoped → a MIXED request uses THIS instance's detail.
        var effectiveDelegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
                .ForInstancesAsync(_db, new[] { vrc.VisitInstanceId }, cancellationToken))
                .GetValueOrDefault(vrc.VisitInstanceId);

        // SEC-10: same fix as the PDF export — was a bare campus-id comparison, skipped entirely for
        // any caller with no PrimaryCampusId, with no relationship or role check at all.
        var acceptedRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == minute.VisitInstanceId && p.UserId == _currentUser.UserId.Value
                && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);
        var (inScope, _) = MinuteAccess.Evaluate(vrc, vrc.VisitRequest, _currentUser, acceptedRole);
        if (!inScope)
        {
            throw new ForbiddenException("Bạn không có quyền tải Excel biên bản của chuyến thăm này.");
        }

        using var workbook = new XLWorkbook();
        
        // Sheet 1: General Info
        var wsGeneral = workbook.Worksheets.Add("Thông tin chung");
        wsGeneral.Cell(1, 1).Value = "Tiêu đề"; wsGeneral.Cell(1, 2).Value = minute.Title;
        wsGeneral.Cell(2, 1).Value = "Đoàn khách"; wsGeneral.Cell(2, 2).Value = effectiveDelegationName ?? "N/A";
        wsGeneral.Cell(3, 1).Value = "Campus"; wsGeneral.Cell(3, 2).Value = campusName ?? "N/A";
        wsGeneral.Cell(4, 1).Value = "Trạng thái"; wsGeneral.Cell(4, 2).Value = minute.Status;
        wsGeneral.Cell(5, 1).Value = "Ngày tạo"; wsGeneral.Cell(5, 2).Value = minute.CreatedAt.ToString("dd/MM/yyyy HH:mm");
        wsGeneral.Cell(6, 1).Value = "Nội dung"; wsGeneral.Cell(6, 2).Value = minute.Content;
        wsGeneral.Columns().AdjustToContents();

        // Sheet 2: Participants
        var wsParticipants = workbook.Worksheets.Add("Người tham gia");
        wsParticipants.Cell(1, 1).Value = "Họ tên";
        wsParticipants.Cell(1, 2).Value = "Vai trò";
        wsParticipants.Cell(1, 3).Value = "Đơn vị";
        wsParticipants.Cell(1, 4).Value = "Điểm danh";
        wsParticipants.Cell(1, 5).Value = "Ghi chú";
        
        int row = 2;
        // Excluded rows are kept in the table so sync remembers the decision (MIN-03); they are not
        // part of the biên bản, so they are not exported as if they had attended.
        foreach (var p in minute.Participants
            .Where(p => p.SyncState != MinuteParticipantSyncStates.Excluded)
            .OrderBy(p => p.DisplayOrder))
        {
            wsParticipants.Cell(row, 1).Value = p.FullNameSnapshot;
            wsParticipants.Cell(row, 2).Value = p.RoleSnapshot;
            wsParticipants.Cell(row, 3).Value = p.OrganizationSnapshot;
            wsParticipants.Cell(row, 4).Value = p.AttendanceStatus;
            wsParticipants.Cell(row, 5).Value = p.AttendanceNote;
            row++;
        }
        wsParticipants.Columns().AdjustToContents();

        // Sheet 3: Action Items
        var wsActions = workbook.Worksheets.Add("Đầu mục công việc");
        wsActions.Cell(1, 1).Value = "Tiêu đề";
        wsActions.Cell(1, 2).Value = "Ghi chú";
        wsActions.Cell(1, 3).Value = "Trạng thái";
        wsActions.Cell(1, 4).Value = "Deadline";
        wsActions.Cell(1, 5).Value = "Ngày hoàn thành";
        
        row = 2;
        foreach (var ai in minute.ActionItems.OrderBy(ai => ai.DisplayOrder))
        {
            wsActions.Cell(row, 1).Value = ai.Title;
            wsActions.Cell(row, 2).Value = ai.Note;
            wsActions.Cell(row, 3).Value = ai.Status;
            wsActions.Cell(row, 4).Value = ai.DueDate?.ToString("dd/MM/yyyy");
            wsActions.Cell(row, 5).Value = ai.CompletedAt?.ToString("dd/MM/yyyy");
            row++;
        }
        wsActions.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

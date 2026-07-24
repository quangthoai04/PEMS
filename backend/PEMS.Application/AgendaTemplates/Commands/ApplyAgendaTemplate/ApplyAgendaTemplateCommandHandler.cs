using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.AgendaTemplates.Commands.ApplyAgendaTemplate;

public sealed class ApplyAgendaTemplateCommandHandler
    : IRequestHandler<ApplyAgendaTemplateCommand, ApplyAgendaTemplateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public ApplyAgendaTemplateCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ApplyAgendaTemplateResponse> Handle(
        ApplyAgendaTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .Include(c => c.FormDetail)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Building the agenda is the official host's job, only during the preparation window
        // (mirrors SaveVisitAgenda). Staff Leader / HO monitor read-only.
        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được áp dụng mẫu lịch trình.");
        if (instance.Status == VisitInstanceStatus.Cancelled)
            throw new ConflictException("Cơ sở này đã bị hủy nên không thể áp dụng mẫu lịch trình.");
        if (instance.Status == VisitInstanceStatus.Closed)
            throw new ConflictException("Cơ sở này đã đóng đoàn nên không thể áp dụng mẫu lịch trình.");
        if (instance.Status != VisitInstanceStatus.Assigned && instance.Status != VisitInstanceStatus.BeforeVisit)
            throw new ConflictException("Chỉ có thể áp dụng mẫu lịch trình trong giai đoạn chuẩn bị (trước tiếp khách).");

        var template = await _db.AgendaTemplates
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.AgendaTemplateId == request.AgendaTemplateId, cancellationToken)
            ?? throw new NotFoundException("AgendaTemplate", request.AgendaTemplateId);

        if (template.DeletedAt != null)
            throw new ConflictException("Mẫu agenda đã bị xóa nên không thể áp dụng.", "AGENDA_TEMPLATE_DELETED");
        if (template.Status != AgendaTemplateStatuses.Active)
            throw new ConflictException("Mẫu agenda đang INACTIVE nên không thể áp dụng.", "AGENDA_TEMPLATE_INACTIVE");
        if (template.Items.Count == 0)
            throw new ConflictException("Mẫu agenda không có mục lịch trình nào để áp dụng.", "AGENDA_TEMPLATE_EMPTY");

        // The host may deliberately apply a template of a different visit_type — allowed, but surfaced.
        // Visit type is per campus, so compare against THIS instance's own detail.
        var requestVisitType = instance.FormDetail?.VisitType;
        bool visitTypeMismatch = template.VisitType != requestVisitType;

        var existing = await _db.VisitAgendas
            .Where(a => a.VisitInstanceId == instance.VisitInstanceId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0 && !request.ReplaceExisting)
            throw new ConflictException(
                "Cơ sở này đã có lịch trình. Bật replaceExisting để thay thế lịch trình hiện tại.",
                "AGENDA_ALREADY_EXISTS");

        var now = _clock.VietnamNow;
        var plannedStart = instance.PlannedStartAt;

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        // Delete the old agenda FIRST and flush, so reused sequence_order values do not trip
        // uq_visit_agendas_order within a single SaveChanges.
        if (existing.Count > 0)
        {
            _db.VisitAgendas.RemoveRange(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var seq = 1;
        var created = new List<VisitAgenda>();
        foreach (var item in template.Items
                     .OrderBy(i => i.StartOffsetMinutes).ThenBy(i => i.DisplayOrder))
        {
            var start = plannedStart.AddMinutes(item.StartOffsetMinutes);
            var end = start.AddMinutes(item.DurationMinutes);
            var entity = new VisitAgenda
            {
                VisitInstanceId = instance.VisitInstanceId,
                SequenceOrder = seq++,
                Title = item.Title,
                Description = item.Description,
                StartTime = start,
                EndTime = end,
                Location = item.Location,
                ResponsibleUserId = null,
                SourceTemplateItemId = item.AgendaTemplateItemId,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            _db.VisitAgendas.Add(entity);
            created.Add(entity);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "APPLY_AGENDA_TEMPLATE",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var items = created
            .OrderBy(a => a.SequenceOrder)
            .Select(a => new AgendaRowView(
                a.AgendaId, a.SequenceOrder, a.Title, a.StartTime, a.EndTime,
                a.Location, a.Description, a.SourceTemplateItemId))
            .ToList();

        var message = visitTypeMismatch
            ? $"Đã áp dụng mẫu agenda (lưu ý: mẫu thuộc loại '{template.VisitType}', khác loại hình của đơn '{requestVisitType}')."
            : "Đã áp dụng mẫu agenda.";

        return new ApplyAgendaTemplateResponse(
            instance.VisitInstanceId, template.AgendaTemplateId, items.Count,
            requestVisitType, template.VisitType, visitTypeMismatch, items, message);
    }
}

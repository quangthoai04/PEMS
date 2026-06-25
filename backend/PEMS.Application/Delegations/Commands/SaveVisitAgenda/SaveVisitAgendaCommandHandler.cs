using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.SaveVisitAgenda;

public sealed class SaveVisitAgendaCommandHandler
    : IRequestHandler<SaveVisitAgendaCommand, SaveVisitAgendaResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public SaveVisitAgendaCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<SaveVisitAgendaResponse> Handle(
        SaveVisitAgendaCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId
                                      && c.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Setup is the official host's job, and only during the preparation window.
        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được chỉnh sửa lịch trình.");
        if (instance.Status == VisitInstanceStatus.Cancelled)
            throw new ConflictException("Cơ sở này đã bị hủy nên không thể chỉnh sửa lịch trình.");
        if (instance.Status == VisitInstanceStatus.Closed)
            throw new ConflictException("Cơ sở này đã đóng đoàn nên không thể chỉnh sửa lịch trình.");
        if (instance.Status != VisitInstanceStatus.Assigned && instance.Status != VisitInstanceStatus.BeforeVisit)
            throw new ConflictException("Chỉ có thể chỉnh sửa lịch trình trong giai đoạn chuẩn bị (trước tiếp khách).");

        var now = _clock.UtcNow;
        var incoming = request.Items ?? new List<SaveVisitAgendaItem>();

        var existing = await _db.VisitAgendas
            .Where(a => a.VisitInstanceId == instance.VisitInstanceId)
            .ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(a => a.AgendaId);

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        // Remove items that the client dropped.
        var keepIds = incoming.Where(i => i.AgendaId.HasValue).Select(i => i.AgendaId!.Value).ToHashSet();
        var toRemove = existing.Where(a => !keepIds.Contains(a.AgendaId)).ToList();
        if (toRemove.Count > 0)
            _db.VisitAgendas.RemoveRange(toRemove);

        var saved = new List<VisitAgenda>();
        var seq = 0;
        foreach (var item in incoming)
        {
            VisitAgenda entity;
            if (item.AgendaId.HasValue && existingById.TryGetValue(item.AgendaId.Value, out var found))
            {
                entity = found; // update in place
                entity.UpdatedAt = now;
                entity.UpdatedBy = actorId;
            }
            else
            {
                entity = new VisitAgenda
                {
                    VisitInstanceId = instance.VisitInstanceId,
                    CreatedAt = now,
                    CreatedBy = actorId,
                };
                _db.VisitAgendas.Add(entity);
            }

            entity.Title = item.Title.Trim();
            entity.StartTime = item.StartTime;
            entity.EndTime = item.EndTime;
            entity.Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim();
            entity.Location = string.IsNullOrWhiteSpace(item.Location) ? null : item.Location.Trim();
            entity.SequenceOrder = seq++;
            saved.Add(entity);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "SAVE_VISIT_AGENDA",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var items = saved
            .OrderBy(a => a.SequenceOrder)
            .Select(a => new SavedAgendaItem(a.AgendaId, a.Title, a.StartTime, a.EndTime, a.Description, a.Location))
            .ToList();

        return new SaveVisitAgendaResponse(instance.VisitInstanceId, items.Count, items, "Đã lưu lịch trình.");
    }
}

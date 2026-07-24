using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
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
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public SaveVisitAgendaCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<SaveVisitAgendaResponse> Handle(
        SaveVisitAgendaCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
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

        var now = _clock.VietnamNow;
        var incoming = request.Items ?? new List<SaveVisitAgendaItem>();

        // ── Responsible-user validation ──
        // A responsible person (when provided) MUST be either the instance's current host or an
        // ACCEPTED participant in a supporting role (IC_SUPPORT / DEPT_SUPPORT / STUDENT), and the
        // user must be ACTIVE. We never trust an arbitrary user id (guards DevTools/Postman tampering);
        // INVITED / ASSIGNED / DECLINED / REMOVED participants are NOT eligible.
        var requestedResponsibleIds = incoming
            .Where(i => i.ResponsibleUserId.HasValue)
            .Select(i => i.ResponsibleUserId!.Value)
            .Distinct()
            .ToList();
        if (requestedResponsibleIds.Count > 0)
        {
            var allowedRoles = new[]
            {
                ParticipantRoles.IcSupport, ParticipantRoles.DeptSupport, ParticipantRoles.Student,
            };

            var activeIds = (await _db.Users
                    .Where(u => requestedResponsibleIds.Contains(u.UserId) && u.Status == UserStatuses.Active)
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var acceptedParticipantIds = await _db.VisitParticipants
                .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                            && p.Status == ParticipantStatuses.Accepted
                            && allowedRoles.Contains(p.ParticipantRole)
                            && requestedResponsibleIds.Contains(p.UserId))
                .Select(p => p.UserId)
                .ToListAsync(cancellationToken);

            var allowed = new HashSet<ulong>(acceptedParticipantIds.Where(id => activeIds.Contains(id)));
            if (instance.CurrentHostUserId.HasValue && activeIds.Contains(instance.CurrentHostUserId.Value))
                allowed.Add(instance.CurrentHostUserId.Value);

            if (requestedResponsibleIds.Any(id => !allowed.Contains(id)))
                throw new BusinessRuleException(
                    "Người phụ trách không hợp lệ hoặc chưa chấp nhận tham gia chuyến tiếp khách này.");
        }

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
            // Real assigned person (null = unassigned). Validated against the candidate set above.
            entity.ResponsibleUserId = item.ResponsibleUserId;
            entity.SequenceOrder = seq++;
            saved.Add(entity);
        }

        // Filed under this instance's own campus/request/instance context so a campus-scoped audit
        // query finds it — the agenda belongs to exactly one campus instance.
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "SAVE_VISIT_AGENDA",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = instance.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            Reason = $"items={incoming.Count}",
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
        // Mixed per-campus v2: notification text uses THIS instance's detail name.
        string delegationName = (await Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_db, new[] { instance.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(instance.VisitInstanceId) ?? "Đoàn khách";
        var agendaActionUrl = $"/dashboard/visit/process/{instance.VisitInstanceId}";

        // Notify Accepted participants
        var notifyParticipantIds = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.Status == ParticipantStatuses.Accepted && p.UserId != actorId)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        foreach (var pId in notifyParticipantIds)
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: pId,
                Title: "Lịch trình được cập nhật",
                Message: $"Lịch trình của đoàn {delegationName} đã được cập nhật.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.AgendaUpdated,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                ActorUserId: actorId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: agendaActionUrl
            ));
        }

        if (notifications.Count > 0)
        {
            await _notificationService.CreateManyAsync(notifications, cancellationToken);
        }

        var items = saved
            .OrderBy(a => a.SequenceOrder)
            .Select(a => new SavedAgendaItem(a.AgendaId, a.Title, a.StartTime, a.EndTime, a.Description, a.Location))
            .ToList();

        return new SaveVisitAgendaResponse(instance.VisitInstanceId, items.Count, items, "Đã lưu lịch trình.");
    }
}

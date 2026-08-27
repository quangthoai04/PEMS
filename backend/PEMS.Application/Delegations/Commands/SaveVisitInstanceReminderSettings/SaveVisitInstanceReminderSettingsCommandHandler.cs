using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Queries.GetVisitInstanceReminderSettings;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Enums;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.SaveVisitInstanceReminderSettings;

/// <summary>
/// Upserts reminder rows. The scheduled send time is computed server-side from the instance's
/// planned start; rows that would fire in the past are rejected (no stale PENDING). enabled=false
/// cancels the matching PENDING row; SENT rows are never modified. Nothing is dispatched here.
/// </summary>
public sealed class SaveVisitInstanceReminderSettingsCommandHandler
    : IRequestHandler<SaveVisitInstanceReminderSettingsCommand, SaveVisitInstanceReminderSettingsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public SaveVisitInstanceReminderSettingsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<SaveVisitInstanceReminderSettingsResponse> Handle(
        SaveVisitInstanceReminderSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (!VisitReminderAccess.CanConfigure(_currentUser, instance))
            throw new ForbiddenException("Bạn không có quyền cập nhật cảnh báo.");

        VisitPreparationGate.EnsurePreparationOpen(instance.Status, "cấu hình cảnh báo");

        var now = _clock.VietnamNow;
        // scheduled_at is Vietnam wall-clock (derived from planned_start_at) — validate "in the past"
        // against Vietnam-local now, not UtcNow.
        var nowVn = _clock.VietnamNow;

        var existingRows = await _db.VisitInstanceReminderSettings
            .Where(r => r.VisitInstanceId == instance.VisitInstanceId)
            .ToListAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            var channel = Enum.Parse<VisitReminderChannel>(item.Channel.Trim(), ignoreCase: true);
            var targetGroup = Enum.Parse<VisitReminderTargetGroup>(item.TargetGroup.Trim(), ignoreCase: true);

            var existing = existingRows.FirstOrDefault(r => r.Channel == channel && r.TargetGroup == targetGroup);

            if (!item.Enabled)
            {
                // Turn off: cancel the matching PENDING row. SENT/FAILED/CANCELLED are left untouched.
                if (existing != null && existing.Status == VisitReminderStatus.PENDING)
                {
                    existing.Status = VisitReminderStatus.CANCELLED;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = actorId;
                }
                continue;
            }

            // scheduled_at = planned_start_at - offset_minutes (a real duration subtraction, so
            // "1 ngày trước" is always the same time of day as the visit itself).
            var scheduledAt = instance.PlannedStartAt.AddMinutes(-item.OffsetMinutes);

            if (scheduledAt >= instance.PlannedStartAt)
                throw new ValidationException(
                    $"Thời điểm gửi cảnh báo ({scheduledAt:HH:mm dd/MM/yyyy}) phải trước thời điểm bắt đầu tiếp khách.");

            if (scheduledAt <= nowVn)
                throw new ValidationException(
                    $"Thời điểm gửi cảnh báo ({scheduledAt:HH:mm dd/MM/yyyy}) đã qua, không thể đặt lịch.");

            if (existing == null)
            {
                var row = new VisitInstanceReminderSetting
                {
                    VisitInstanceId = instance.VisitInstanceId,
                    Channel = channel,
                    TargetGroup = targetGroup,
                    OffsetMinutes = item.OffsetMinutes,
                    ScheduledAt = scheduledAt,
                    Status = VisitReminderStatus.PENDING,
                    CreatedAt = now,
                    CreatedBy = actorId,
                };
                _db.VisitInstanceReminderSettings.Add(row);
                existingRows.Add(row);
            }
            else if (existing.Status != VisitReminderStatus.SENT)
            {
                // Re-arm/update a PENDING/CANCELLED/FAILED row. Never modify a SENT row.
                existing.OffsetMinutes = item.OffsetMinutes;
                existing.ScheduledAt = scheduledAt;
                existing.Status = VisitReminderStatus.PENDING;
                existing.ErrorMessage = null;
                existing.UpdatedAt = now;
                existing.UpdatedBy = actorId;
            }
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = instance.CampusId,
            Action = "SAVE_VISIT_REMINDER_SETTINGS",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        var rows = await _db.VisitInstanceReminderSettings
            .Where(r => r.VisitInstanceId == instance.VisitInstanceId)
            .OrderBy(r => r.Channel).ThenBy(r => r.TargetGroup)
            .ToListAsync(cancellationToken);

        return new SaveVisitInstanceReminderSettingsResponse
        {
            Items = rows.Select(r => new VisitReminderSettingDto
            {
                ReminderSettingId = r.ReminderSettingId,
                Channel = r.Channel.ToString(),
                TargetGroup = r.TargetGroup.ToString(),
                OffsetMinutes = r.OffsetMinutes,
                ScheduledAt = r.ScheduledAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                Status = r.Status.ToString(),
            }).ToList(),
            Message = "Đã lưu cấu hình cảnh báo.",
        };
    }
}

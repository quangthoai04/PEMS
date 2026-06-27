using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Enums;

namespace PEMS.Application.Delegations.Commands.CancelVisitInstanceReminderSettings;

public sealed class CancelVisitInstanceReminderSettingsCommandHandler
    : IRequestHandler<CancelVisitInstanceReminderSettingsCommand, CancelVisitInstanceReminderSettingsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CancelVisitInstanceReminderSettingsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CancelVisitInstanceReminderSettingsResponse> Handle(
        CancelVisitInstanceReminderSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (!VisitReminderAccess.CanConfigure(_currentUser, instance))
            throw new ForbiddenException("Bạn không có quyền cập nhật cảnh báo.");

        var pending = await _db.VisitInstanceReminderSettings
            .Where(r => r.VisitInstanceId == instance.VisitInstanceId
                        && r.Status == VisitReminderStatus.PENDING)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        foreach (var row in pending)
        {
            row.Status = VisitReminderStatus.CANCELLED;
            row.UpdatedAt = now;
            row.UpdatedBy = actorId;
        }

        if (pending.Count > 0)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = instance.CampusId,
                Action = "CANCEL_VISIT_REMINDER_SETTINGS",
                EntityType = "VisitRequestCampus",
                EntityId = instance.VisitInstanceId,
                CreatedAt = now,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new CancelVisitInstanceReminderSettingsResponse(pending.Count, "Đã hủy lịch gửi cảnh báo.");
    }
}

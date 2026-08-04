using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceReminderSettings;

public sealed class GetVisitInstanceReminderSettingsQueryHandler
    : IRequestHandler<GetVisitInstanceReminderSettingsQuery, GetVisitInstanceReminderSettingsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitInstanceReminderSettingsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetVisitInstanceReminderSettingsResponse> Handle(
        GetVisitInstanceReminderSettingsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (!VisitReminderAccess.CanView(_currentUser, instance))
            throw new ForbiddenException("Bạn không có quyền xem cấu hình cảnh báo.");

        var rows = await _db.VisitInstanceReminderSettings
            .Where(r => r.VisitInstanceId == instance.VisitInstanceId)
            .OrderBy(r => r.Channel).ThenBy(r => r.TargetGroup)
            .ToListAsync(cancellationToken);

        return new GetVisitInstanceReminderSettingsResponse
        {
            Items = rows.Select(r => new VisitReminderSettingDto
            {
                ReminderSettingId = r.ReminderSettingId,
                Channel = r.Channel.ToString(),
                TargetGroup = r.TargetGroup.ToString(),
                DaysBefore = r.DaysBefore,
                ReminderTime = $"{r.ReminderTime.Hours:D2}:{r.ReminderTime.Minutes:D2}",
                ScheduledAt = r.ScheduledAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                Status = r.Status.ToString(),
                ErrorMessage = r.ErrorMessage,
            }).ToList(),
        };
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceLogistics;

public sealed class GetVisitInstanceLogisticsQueryHandler
    : IRequestHandler<GetVisitInstanceLogisticsQuery, GetVisitInstanceLogisticsResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitInstanceLogisticsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetVisitInstanceLogisticsResponse> Handle(
        GetVisitInstanceLogisticsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (!VisitReminderAccess.CanView(_currentUser, instance))
            throw new ForbiddenException("Bạn không có quyền xem danh sách yêu cầu hậu cần.");

        var items = await _db.VisitLogisticsItems
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
            .OrderByDescending(l => l.LogisticsItemId)
            .Select(l => new VisitInstanceLogisticsItemDto
            {
                LogisticsItemId = l.LogisticsItemId,
                ItemType = l.ItemType,
                Title = l.Title,
                Description = l.Description,
                Quantity = l.Quantity,
                Status = l.Status,
                Priority = l.Priority,
                RequestedToDepartmentId = l.RequestedToDepartmentId,
                AssignedToUserId = l.AssignedToUserId,
            })
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new GetVisitInstanceLogisticsResponse();

        // Enrich department + assignee names + format wall-clock times in-memory (avoid correlated
        // subqueries on optional FKs — Pomelo translation pitfall).
        var rows = await _db.VisitLogisticsItems
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
            .Select(l => new { l.LogisticsItemId, l.RequestedAt, l.UsageStartAt, l.UsageEndAt, l.DueAt })
            .ToListAsync(cancellationToken);
        var timeById = rows.ToDictionary(r => r.LogisticsItemId);

        var deptIds = items.Where(i => i.RequestedToDepartmentId.HasValue)
            .Select(i => i.RequestedToDepartmentId!.Value).Distinct().ToList();
        var deptNames = deptIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Departments.Where(d => deptIds.Contains(d.DepartmentId))
                .ToDictionaryAsync(d => d.DepartmentId, d => d.Name, cancellationToken);

        var userIds = items.Where(i => i.AssignedToUserId.HasValue)
            .Select(i => i.AssignedToUserId!.Value).Distinct().ToList();
        var userNames = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        foreach (var i in items)
        {
            if (i.RequestedToDepartmentId.HasValue && deptNames.TryGetValue(i.RequestedToDepartmentId.Value, out var dn))
                i.DepartmentName = dn;
            if (i.AssignedToUserId.HasValue && userNames.TryGetValue(i.AssignedToUserId.Value, out var un))
                i.AssignedToName = un;
            if (timeById.TryGetValue(i.LogisticsItemId, out var t))
            {
                i.RequestedAt = t.RequestedAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                i.UsageStartAt = t.UsageStartAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                i.UsageEndAt = t.UsageEndAt?.ToString("yyyy-MM-ddTHH:mm:ss");
                i.DueAt = t.DueAt?.ToString("yyyy-MM-ddTHH:mm:ss");
            }
        }

        return new GetVisitInstanceLogisticsResponse { Items = items };
    }
}

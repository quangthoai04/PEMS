using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using PEMS.Application.Common;
namespace PEMS.Application.DepartmentReceptionTasks.Queries.GetOverdueLogisticsResponses;

/// <summary>
/// Logistics requests whose response deadline (due_at, auto-computed as usage_start - 24h — never
/// user-facing) has passed while still awaiting the caller's response:
///   Dept Leader → REQUESTED items sent to their department, not yet assigned to anyone.
///   Dept Staff  → ASSIGNED items assigned to them, not yet accepted/declined.
/// Drives the forced-response gate (must respond before the dashboard is usable again).
/// </summary>
public sealed class GetOverdueLogisticsResponsesQuery : IRequest<OverdueLogisticsResponseQueueDto>
{
}

public sealed class OverdueLogisticsResponseQueueDto
{
    public List<OverdueLogisticsResponseItemDto> Items { get; set; } = new();
}

public sealed class OverdueLogisticsResponseItemDto
{
    public ulong LogisticsItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime DueAt { get; set; }
}

public sealed class GetOverdueLogisticsResponsesQueryHandler
    : IRequestHandler<GetOverdueLogisticsResponsesQuery, OverdueLogisticsResponseQueueDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetOverdueLogisticsResponsesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OverdueLogisticsResponseQueueDto> Handle(
        GetOverdueLogisticsResponsesQuery request, CancellationToken cancellationToken)
    {
        var empty = new OverdueLogisticsResponseQueueDto();

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null || _currentUser.DepartmentId is null)
            return empty;
        if (!string.Equals(_currentUser.RoleCode, RoleCodes.Department, StringComparison.OrdinalIgnoreCase))
            return empty;

        var now = VietnamTime.Now();
        var userId = _currentUser.UserId.Value;
        var departmentId = _currentUser.DepartmentId.Value;

        IQueryable<Domain.Entities.Delegations.VisitLogisticsItem> query;
        if (string.Equals(_currentUser.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase))
        {
            query = _db.VisitLogisticsItems.AsNoTracking().Where(li =>
                li.RequestedToDepartmentId == departmentId
                && li.Status == "REQUESTED"
                && li.AssignedToUserId == null
                && li.DueAt != null && li.DueAt < now);
        }
        else if (string.Equals(_currentUser.SubRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase))
        {
            query = _db.VisitLogisticsItems.AsNoTracking().Where(li =>
                li.AssignedToUserId == userId
                && li.Status == "ASSIGNED"
                && li.DueAt != null && li.DueAt < now);
        }
        else
        {
            return empty;
        }

        var items = await query
            .OrderBy(li => li.DueAt)
            .Select(li => new OverdueLogisticsResponseItemDto
            {
                LogisticsItemId = li.LogisticsItemId,
                Title = li.Title,
                DueAt = li.DueAt!.Value,
            })
            .ToListAsync(cancellationToken);

        return new OverdueLogisticsResponseQueueDto { Items = items };
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Admin.Queries.GetAdminAuditLogs;

public sealed class GetAdminAuditLogsQueryHandler
    : IRequestHandler<GetAdminAuditLogsQuery, PaginatedResult<AdminAuditLogItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminAuditLogsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<AdminAuditLogItemDto>> Handle(
        GetAdminAuditLogsQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim().ToLower();
            query = query.Where(a => a.ActorUser != null &&
                (a.ActorUser.Email.ToLower().Contains(kw) || a.ActorUser.FullName.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var entityType = request.EntityType.Trim();
            query = query.Where(a => a.EntityType == entityType);
        }

        if (request.CampusId.HasValue)
            query = query.Where(a => a.CampusId == request.CampusId.Value);

        if (request.FromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= request.FromDate.Value);
        if (request.ToDate.HasValue)
        {
            var toExclusive = request.ToDate.Value.Date.AddDays(1);
            query = query.Where(a => a.CreatedAt < toExclusive);
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var campusNames = await _db.Campuses.AsNoTracking()
            .Select(c => new { c.CampusId, c.Name })
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        var rows = await query
            .OrderByDescending(a => a.AuditLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.AuditLogId,
                a.ActorUserId,
                ActorName = a.ActorUser != null ? a.ActorUser.FullName : null,
                ActorEmail = a.ActorUser != null ? a.ActorUser.Email : null,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.CampusId,
                a.IpAddress,
                a.RequestId,
                ChangeCount = a.Changes.Count,
                a.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(a => new AdminAuditLogItemDto
        {
            AuditLogId = a.AuditLogId,
            ActorUserId = a.ActorUserId,
            ActorName = a.ActorName,
            ActorEmail = a.ActorEmail,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            CampusId = a.CampusId,
            CampusName = a.CampusId.HasValue && campusNames.TryGetValue(a.CampusId.Value, out var name) ? name : null,
            IpAddress = a.IpAddress,
            RequestId = a.RequestId,
            ChangeCount = a.ChangeCount,
            CreatedAt = a.CreatedAt,
        }).ToList();

        return PaginatedResult<AdminAuditLogItemDto>.Create(items, page, pageSize, totalItems);
    }
}

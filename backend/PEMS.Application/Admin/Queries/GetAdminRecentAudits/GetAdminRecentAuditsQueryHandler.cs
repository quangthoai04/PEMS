using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Admin.Queries.GetAdminRecentAudits;

public sealed class GetAdminRecentAuditsQueryHandler
    : IRequestHandler<GetAdminRecentAuditsQuery, List<AdminRecentAuditItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminRecentAuditsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<AdminRecentAuditItemDto>> Handle(
        GetAdminRecentAuditsQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var limit = Math.Clamp(request.Limit, 1, 50);
        var campusNames = await _db.Campuses.AsNoTracking()
            .Select(c => new { c.CampusId, c.Name })
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        var rows = await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.AuditLogId)
            .Take(limit)
            .Select(a => new
            {
                a.AuditLogId,
                ActorName = a.ActorUser != null ? a.ActorUser.FullName : null,
                ActorEmail = a.ActorUser != null ? a.ActorUser.Email : null,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.CampusId,
                a.IpAddress,
                a.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(a => new AdminRecentAuditItemDto
        {
            AuditLogId = a.AuditLogId,
            ActorName = a.ActorName,
            ActorEmail = a.ActorEmail,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            CampusName = a.CampusId.HasValue && campusNames.TryGetValue(a.CampusId.Value, out var name) ? name : null,
            IpAddress = a.IpAddress,
            CreatedAt = a.CreatedAt,
        }).ToList();
    }
}

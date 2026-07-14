using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Admin.Queries.GetAdminSecurityOverview;

public sealed class GetAdminSecurityOverviewQueryHandler
    : IRequestHandler<GetAdminSecurityOverviewQuery, AdminSecurityOverviewDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetAdminSecurityOverviewQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AdminSecurityOverviewDto> Handle(
        GetAdminSecurityOverviewQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var from = _clock.VietnamNow.AddDays(-7);

        var counts = await _db.SecurityEvents.AsNoTracking()
            .Where(e => e.CreatedAt >= from)
            .GroupBy(e => e.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var recent = await _db.SecurityEvents.AsNoTracking()
            .Where(e => e.Severity == "HIGH" || e.Severity == "CRITICAL")
            .OrderByDescending(e => e.CreatedAt)
            .Take(10)
            .Select(e => new AdminSecurityEventItemDto
            {
                SecurityEventId = e.SecurityEventId,
                EventType = e.EventType,
                Result = e.Result,
                Severity = e.Severity,
                Email = e.EmailSnapshot ?? (e.User != null ? e.User.Email : null),
                IpAddress = e.IpAddress,
                LoginPortal = e.LoginPortal,
                ProviderType = e.ProviderType,
                CreatedAt = e.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        int Count(string severity) => counts.Where(c => c.Severity == severity).Sum(c => c.Count);

        return new AdminSecurityOverviewDto
        {
            Low = Count("LOW"),
            Medium = Count("MEDIUM"),
            High = Count("HIGH"),
            Critical = Count("CRITICAL"),
            RecentHighSeverity = recent,
        };
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

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
            .Where(e => e.Severity == SecuritySeverities.High || e.Severity == SecuritySeverities.Critical)
            .OrderByDescending(e => e.CreatedAt)
            .Take(10)
            .Select(e => new AdminSecurityEventItemDto
            {
                SecurityEventId = e.SecurityEventId,
                EventType = e.EventType,
                Result = e.Result,
                Severity = e.Severity,
                // Replaces the provider column: WHY an event was refused says far more about a
                // HIGH/CRITICAL row than which sign-in button was pressed (that lives in login_logs).
                FailureReasonCode = e.FailureReasonCode,
                Email = e.EmailSnapshot ?? (e.User != null ? e.User.Email : null),
                IpAddress = e.IpAddress,
                LoginPortal = e.LoginPortal,
                CreatedAt = e.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        int Count(string severity) => counts.Where(c => c.Severity == severity).Sum(c => c.Count);

        return new AdminSecurityOverviewDto
        {
            Low = Count(SecuritySeverities.Low),
            Medium = Count(SecuritySeverities.Medium),
            High = Count(SecuritySeverities.High),
            Critical = Count(SecuritySeverities.Critical),
            RecentHighSeverity = recent,
        };
    }
}

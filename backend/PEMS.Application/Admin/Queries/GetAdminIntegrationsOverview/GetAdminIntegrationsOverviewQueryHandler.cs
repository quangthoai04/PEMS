using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Admin.Queries.GetAdminIntegrationsOverview;

public sealed class GetAdminIntegrationsOverviewQueryHandler
    : IRequestHandler<GetAdminIntegrationsOverviewQuery, List<AdminApiRequestActivityPointDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetAdminIntegrationsOverviewQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<List<AdminApiRequestActivityPointDto>> Handle(
        GetAdminIntegrationsOverviewQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var days = Math.Clamp(request.Days, 1, 90);
        var today = _clock.VietnamNow.Date;
        var from = today.AddDays(-(days - 1));

        var grouped = await _db.ApiRequestLogs.AsNoTracking()
            .Where(l => l.CreatedAt >= from)
            .GroupBy(l => new { l.CreatedAt.Date, l.Success })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.Success,
                Count = g.Count(),
                AvgMs = g.Average(x => (double?)x.ResponseTimeMs),
            })
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, days)
            .Select(offset =>
            {
                var day = from.AddDays(offset);
                var dayRows = grouped.Where(g => g.Date == day).ToList();
                var weighted = dayRows.Where(r => r.AvgMs.HasValue).ToList();
                var totalWeighted = weighted.Sum(r => r.Count);
                return new AdminApiRequestActivityPointDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Success = dayRows.Where(r => r.Success).Sum(r => r.Count),
                    Failed = dayRows.Where(r => !r.Success).Sum(r => r.Count),
                    AvgResponseTimeMs = totalWeighted > 0
                        ? (int)Math.Round(weighted.Sum(r => r.AvgMs!.Value * r.Count) / totalWeighted)
                        : null,
                };
            })
            .ToList();
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Admin.Queries.GetAdminLoginActivity;

public sealed class GetAdminLoginActivityQueryHandler
    : IRequestHandler<GetAdminLoginActivityQuery, List<AdminLoginActivityPointDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetAdminLoginActivityQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<List<AdminLoginActivityPointDto>> Handle(
        GetAdminLoginActivityQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        var days = Math.Clamp(request.Days, 1, 90);
        var today = _clock.VietnamNow.Date;
        var from = today.AddDays(-(days - 1));

        var grouped = await _db.LoginLogs.AsNoTracking()
            .Where(l => l.CreatedAt >= from)
            .GroupBy(l => new { l.CreatedAt.Date, IsSuccess = l.Status == "SUCCESS" })
            .Select(g => new { g.Key.Date, g.Key.IsSuccess, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Fill every day in range so the chart never has holes.
        return Enumerable.Range(0, days)
            .Select(offset =>
            {
                var day = from.AddDays(offset);
                return new AdminLoginActivityPointDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Success = grouped.Where(g => g.Date == day && g.IsSuccess).Sum(g => g.Count),
                    Failed = grouped.Where(g => g.Date == day && !g.IsSuccess).Sum(g => g.Count),
                };
            })
            .ToList();
    }
}

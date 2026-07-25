using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

using PEMS.Application.Common;
namespace PEMS.Application.Dashboard.Queries.GetHODashboardOverview;

public class GetHODashboardOverviewQueryHandler
    : IRequestHandler<GetHODashboardOverviewQuery, HODashboardOverviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetHODashboardOverviewQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<HODashboardOverviewDto> Handle(
        GetHODashboardOverviewQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.RoleCode != "HO")
        {
            throw new ForbiddenException("Only HO users can access this dashboard.");
        }

        var now = VietnamTime.Now();
        var in7Days = now.AddDays(7);

        var dto = new HODashboardOverviewDto();

        // 1. KPIs
        dto.Kpis.PendingRequests = await _context.VisitRequests
            .Where(r => r.Status == VisitRequestStatuses.PendingApproval)
            .CountAsync(cancellationToken);

        dto.Kpis.OverdueRequests = await _context.VisitRequests
            .Where(r => r.Status == VisitRequestStatuses.PendingApproval && r.CreatedAt < now.AddHours(-48))
            .CountAsync(cancellationToken);

        dto.Kpis.UpcomingVisits = await _context.VisitRequestCampuses
            .Where(c => c.PlannedStartAt >= now && c.PlannedStartAt <= in7Days && (c.Status == VisitInstanceStatuses.BeforeVisit || c.Status == VisitInstanceStatuses.Assigned))
            .CountAsync(cancellationToken);

        dto.Kpis.LowFeedback = await _context.Feedbacks
            .Where(f => f.Rating <= 2)
            .CountAsync(cancellationToken);

        // 2. Action Items
        if (dto.Kpis.OverdueRequests > 0)
        {
            dto.ActionItems.Add(new HOActionItemDto
            {
                Title = "Đơn chờ duyệt quá hạn",
                Desc = $"Có {dto.Kpis.OverdueRequests} đơn liên cơ sở chờ duyệt quá 48h."
            });
        }
        if (dto.Kpis.LowFeedback > 0)
        {
            dto.ActionItems.Add(new HOActionItemDto
            {
                Title = "Feedback thấp cần xem xét",
                Desc = $"Hệ thống ghi nhận {dto.Kpis.LowFeedback} đánh giá chất lượng dưới mức tiêu chuẩn."
            });
        }

        // 3. Pending Requests
        dto.PendingRequests = await _context.VisitRequests
            .Where(r => r.Status == VisitRequestStatuses.PendingApproval)
            .OrderBy(r => r.CreatedAt)
            .Take(5)
            .Select(r => new HOPendingRequestDto
            {
                Id = r.VisitRequestId.ToString(),
                // Request-level row: a MIXED v2 request has no single business name (plan §8.3).
                Name = r.HasMixedCampusDetails ? "Khác nhau theo cơ sở" : (r.CampusInstances.Select(ci => ci.FormDetail.DelegationName).FirstOrDefault() ?? r.RequestCode ?? "Đoàn khách"),
                Campus = "Nhiều cơ sở"
            })
            .ToListAsync(cancellationToken);

        // 4. Upcoming Visits
        var upcoming = await (from c in _context.VisitRequestCampuses
                              join cp in _context.Campuses on c.CampusId equals cp.CampusId
                              where c.PlannedStartAt >= now && (c.Status == VisitInstanceStatuses.BeforeVisit || c.Status == VisitInstanceStatuses.Assigned)
                              orderby c.PlannedStartAt
                              select new
                              {
                                  // Instance row: a MIXED v2 visit shows THIS campus's detail name.
                                  Name = c.FormDetail != null ? c.FormDetail.DelegationName : null,
                                  Campus = cp.Name,
                                  Date = c.PlannedStartAt
                              })
                              .Take(5)
                              .ToListAsync(cancellationToken);

        dto.UpcomingVisits = upcoming.Select(u => new HOUpcomingVisitDto
        {
            Name = u.Name,
            Campus = u.Campus ?? "Unknown",
            Date = u.Date.ToString("dd/MM/yyyy HH:mm")
        }).ToList();

        // 5. Campus Status
        var allCampuses = await _context.Campuses.Where(c => c.Status == "ACTIVE").ToListAsync(cancellationToken);
        foreach (var campus in allCampuses)
        {
            var processing = await _context.VisitRequestCampuses
                .Where(c => c.CampusId == campus.CampusId && c.Status == VisitInstanceStatuses.DuringVisit)
                .CountAsync(cancellationToken);

            var upcomingCount = await _context.VisitRequestCampuses
                .Where(c => c.CampusId == campus.CampusId && c.PlannedStartAt >= now && (c.Status == VisitInstanceStatuses.BeforeVisit || c.Status == VisitInstanceStatuses.Assigned))
                .CountAsync(cancellationToken);

            var alerts = await (from f in _context.Feedbacks
                                join vrc in _context.VisitRequestCampuses on f.VisitInstanceId equals vrc.VisitInstanceId
                                where vrc.CampusId == campus.CampusId && f.Rating <= 2
                                select f.FeedbackId).CountAsync(cancellationToken);

            dto.CampusStatus.Add(new HOCampusStatusDto
            {
                Name = campus.Name,
                Processing = processing,
                Upcoming = upcomingCount,
                Alerts = alerts
            });
        }

        // 6. Recent Activities
        var recentLogs = await _context.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new
            {
                a.Action,
                a.EntityType,
                a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        dto.RecentActivities = recentLogs.Select(l => new HORecentActivityDto
        {
            Content = $"{l.Action} {l.EntityType}",
            Time = l.CreatedAt.ToString("dd/MM/yyyy HH:mm")
        }).ToList();

        return dto;
    }
}

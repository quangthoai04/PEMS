using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Spec §5 (HO rule "campus gần đến ngày tham quan nhưng chưa xử lý"): alerts every ACTIVE HO
/// user once per campus instance when a multi-campus visit request still has a campus sitting in
/// WAITING_REQUEST_APPROVAL within 24h of its planned start. DedupeKey guarantees exactly one
/// alert per campus instance regardless of how many ticks see it before it's resolved or expires.
/// </summary>
public sealed class HoUnprocessedCampusAlertHostedService : BackgroundService
{
    private static readonly TimeSpan UrgentWindow = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HoUnprocessedCampusAlertHostedService> _logger;
    private readonly TimeSpan _pollInterval;

    public HoUnprocessedCampusAlertHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<HoUnprocessedCampusAlertHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = int.TryParse(configuration["Reminders:HoAlertPollSeconds"], out var s) && s > 0 ? s : 600;
        _pollInterval = TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchDueAlertsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HO unprocessed-campus alert tick failed.");
            }

            try { await Task.Delay(_pollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DispatchDueAlertsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        // planned_start_at is Vietnam wall-clock — compare against VietnamNow (never UtcNow).
        var now = clock.VietnamNow;
        var threshold = now.Add(UrgentWindow);

        var due = await (
            from c in db.VisitRequestCampuses
            join r in db.VisitRequests on c.VisitRequestId equals r.VisitRequestId
            where c.Status == VisitInstanceStatus.WaitingRequestApproval
                  && r.VisitScope == VisitScopes.MultiCampus
                  && c.PlannedStartAt <= threshold
            // The alert is about ONE unprocessed campus instance → name it from that instance's detail.
            select new
            {
                c.VisitInstanceId,
                c.CampusId,
                c.PlannedStartAt,
                r.VisitRequestId,
                r.RequestCode,
                DelegationName = c.FormDetail != null ? c.FormDetail.DelegationName : null
            })
            .Take(100)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        var hoUsers = await db.Users
            .Where(u => u.Role.RoleCode == RoleCodes.Ho && u.Status == "ACTIVE")
            .Select(u => u.UserId)
            .ToListAsync(ct);
        if (hoUsers.Count == 0) return;

        var campusNames = await db.Campuses
            .Where(c => due.Select(d => d.CampusId).Distinct().Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, ct);

        var requests = new List<CreateNotificationRequest>();
        foreach (var item in due)
        {
            var campusName = campusNames.TryGetValue(item.CampusId, out var name) ? name : $"#{item.CampusId}";
            var plannedText = item.PlannedStartAt.ToString("HH:mm dd/MM/yyyy");

            requests.AddRange(hoUsers.Select(id => new CreateNotificationRequest(
                RecipientUserId: id,
                Title: "Campus chưa xử lý sắp đến ngày tham quan",
                Message: $"Cơ sở {campusName} của đơn {item.RequestCode} ({item.DelegationName}) vẫn chưa xử lý, dự kiến diễn ra lúc {plannedText}.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                RelatedId: item.VisitInstanceId,
                Category: NotificationCategories.Visit,
                IsActionRequired: true,
                VisitRequestId: item.VisitRequestId,
                VisitInstanceId: item.VisitInstanceId,
                CampusId: item.CampusId,
                ActionType: NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit/process/{item.VisitInstanceId}",
                DedupeKey: $"HO_CAMPUS_UNPROCESSED_{item.VisitInstanceId}",
                MetadataJson: NotificationEventKeys.BuildMetadata(
                    NotificationEventKeys.HoCampusUnprocessedAlert,
                    new { delegationName = item.DelegationName, campusName, requestCode = item.RequestCode })
            )));
        }

        await notificationService.CreateManyAsync(requests, ct);
    }
}

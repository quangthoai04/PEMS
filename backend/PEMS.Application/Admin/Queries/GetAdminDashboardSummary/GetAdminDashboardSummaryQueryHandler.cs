using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Admin.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Admin.Queries.GetAdminDashboardSummary;

public sealed class GetAdminDashboardSummaryQueryHandler
    : IRequestHandler<GetAdminDashboardSummaryQuery, AdminDashboardSummaryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetAdminDashboardSummaryQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AdminDashboardSummaryDto> Handle(
        GetAdminDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        AdminAccess.EnsureAdmin(_currentUser);

        // Session/log tables store Vietnam wall-clock values (see SessionService/SecurityAuditService).
        var now = _clock.VietnamNow;
        var last24h = now.AddHours(-24);
        var last7d = now.AddDays(-7);
        var last30d = now.AddDays(-30);
        var currentPeriod = now.ToString("yyyyMM");

        var accountGroups = await _db.Users.AsNoTracking()
            .GroupBy(u => u.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var newAccounts = await _db.Users.AsNoTracking()
            .CountAsync(u => u.CreatedAt >= last30d, cancellationToken);

        var activeSessions = await _db.UserSessions.AsNoTracking()
            .CountAsync(s => s.RevokedAt == null && s.ExpiresAt > now, cancellationToken);
        var expiredSessions = await _db.UserSessions.AsNoTracking()
            .CountAsync(s => s.RevokedAt == null && s.ExpiresAt <= now, cancellationToken);
        var revokedSessions = await _db.UserSessions.AsNoTracking()
            .CountAsync(s => s.RevokedAt != null, cancellationToken);

        var loginSuccess = await _db.LoginLogs.AsNoTracking()
            .CountAsync(l => l.CreatedAt >= last24h && l.Status == "SUCCESS", cancellationToken);
        var loginFailed = await _db.LoginLogs.AsNoTracking()
            .CountAsync(l => l.CreatedAt >= last24h && l.Status != "SUCCESS", cancellationToken);

        var highEvents = await _db.SecurityEvents.AsNoTracking()
            .CountAsync(e => e.CreatedAt >= last7d && e.Severity == "HIGH", cancellationToken);
        var criticalEvents = await _db.SecurityEvents.AsNoTracking()
            .CountAsync(e => e.CreatedAt >= last7d && e.Severity == "CRITICAL", cancellationToken);

        var integrations = await _db.ApiConfigurations.AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .Select(c => new
            {
                c.ApiConfigId,
                c.Status,
                c.LastTestStatus,
                HasCredential = c.CredentialsJsonEncrypted != null && c.CredentialsJsonEncrypted != "",
                HasSecretRef = c.SecretRef != null && c.SecretRef != "",
            })
            .ToListAsync(cancellationToken);

        var quotaAbove80 = await _db.ApiUsageQuotas.AsNoTracking()
            .CountAsync(q => q.PeriodYyyymm == currentPeriod
                             && q.MonthlyLimit > 0
                             && q.UsedCount * 100 >= q.MonthlyLimit * 80, cancellationToken);

        int CountStatus(string status) =>
            accountGroups.Where(g => g.Status == status).Sum(g => g.Count);

        return new AdminDashboardSummaryDto
        {
            Accounts = new AdminDashboardSummaryDto.AccountSummaryDto
            {
                Total = accountGroups.Sum(g => g.Count),
                Active = CountStatus(UserStatuses.Active),
                Inactive = CountStatus(UserStatuses.Inactive),
                Locked = CountStatus(UserStatuses.Locked),
                NewLast30Days = newAccounts,
            },
            Sessions = new AdminDashboardSummaryDto.SessionSummaryDto
            {
                Active = activeSessions,
                Expired = expiredSessions,
                Revoked = revokedSessions,
            },
            Logins24h = new AdminDashboardSummaryDto.LoginSummaryDto
            {
                Success = loginSuccess,
                Failed = loginFailed,
            },
            Security = new AdminDashboardSummaryDto.SecuritySummaryDto
            {
                HighLast7Days = highEvents,
                CriticalLast7Days = criticalEvents,
            },
            Integrations = new AdminDashboardSummaryDto.IntegrationSummaryDto
            {
                Total = integrations.Count,
                Active = integrations.Count(c => c.Status == "ACTIVE"),
                TestFailed = integrations.Count(c => c.LastTestStatus == "FAILED"),
                MissingCredential = integrations.Count(c => !c.HasCredential && !c.HasSecretRef),
                QuotaAbove80Percent = quotaAbove80,
            },
        };
    }
}

using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Logging;

/// <summary>
/// Persists authentication audit trails (login_logs + security_events). Each call
/// saves immediately so records survive even when the request later fails.
/// </summary>
public sealed class SecurityAuditService : ISecurityAuditService
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;

    public SecurityAuditService(IApplicationDbContext db, IDateTimeService clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task WriteLoginLogAsync(
        ulong? userId,
        string email,
        string loginPortal,
        string? providerType,
        string status,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var entry = new LoginLog
        {
            UserId = userId,
            Email = Truncate(email, 150) ?? string.Empty,
            LoginPortal = loginPortal,
            ProviderType = providerType,
            Status = status,
            FailureReason = Truncate(failureReason, 255),
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 500),
            CreatedAt = _clock.VietnamNow
        };

        _db.LoginLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task WriteSecurityEventAsync(
        ulong? userId,
        string? emailSnapshot,
        string eventType,
        string result,
        string? failureReasonCode = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? loginPortal = null,
        string? detailText = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new SecurityEvent
        {
            UserId = userId,
            EmailSnapshot = Truncate(emailSnapshot, 150),
            EventType = Truncate(eventType, 80) ?? eventType,
            Result = result,
            FailureReasonCode = Truncate(failureReasonCode, 80),
            // Derived, never passed in — one scale for every producer (see SecuritySeverityResolver).
            Severity = SecuritySeverityResolver.Resolve(eventType, result, failureReasonCode),
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 500),
            LoginPortal = loginPortal,
            DetailText = detailText,
            CreatedAt = _clock.VietnamNow
        };

        _db.SecurityEvents.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}

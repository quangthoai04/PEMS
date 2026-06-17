using PEMS.Application.Common.Interfaces;
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
        string? userId,
        string email,
        string loginPortal,
        string? selectedCampusId,
        string? providerType,
        string status,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        string? sessionId,
        CancellationToken cancellationToken = default)
    {
        var entry = new LoginLog
        {
            UserId = userId,
            Email = Truncate(email, 150) ?? string.Empty,
            LoginPortal = loginPortal,
            SelectedCampusId = selectedCampusId,
            ProviderType = providerType,
            Status = status,
            FailureReason = Truncate(failureReason, 255),
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 500),
            SessionId = sessionId,
            CreatedAt = _clock.UtcNow
        };

        _db.LoginLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task WriteSecurityEventAsync(
        string? userId,
        string? email,
        string eventType,
        string severity,
        string? ipAddress,
        string? userAgent,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new SecurityEvent
        {
            UserId = userId,
            Email = Truncate(email, 150),
            EventType = Truncate(eventType, 80) ?? eventType,
            Severity = severity,
            IpAddress = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 500),
            Metadata = metadata,
            CreatedAt = _clock.UtcNow
        };

        _db.SecurityEvents.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}

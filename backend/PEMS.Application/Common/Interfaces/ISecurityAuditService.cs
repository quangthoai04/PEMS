namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Writes audit trails for authentication events. Each method persists
/// immediately so the record survives even when the surrounding request fails
/// (e.g. a failed-login row must be written before the 401 is thrown).
/// </summary>
public interface ISecurityAuditService
{
    /// <summary>
    /// Appends a <c>login_logs</c> row. Provider/portal/IP are the audit dimensions; the campus is
    /// NOT one of them — an account has exactly one primary campus, so recording it per login row
    /// duplicated <c>users.primary_campus_id</c> without adding a fact.
    /// </summary>
    Task WriteLoginLogAsync(
        ulong? userId,
        string email,
        string loginPortal,
        string? providerType,
        string status,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a <c>security_events</c> row. Severity is NOT a parameter: it is derived centrally
    /// from eventType + result + failureReasonCode by
    /// <c>PEMS.Application.Common.Security.SecuritySeverityResolver</c>, so every producer lands on
    /// the same scale. Campus-scoped events carry their campus id inside
    /// <paramref name="detailText"/>.
    /// </summary>
    Task WriteSecurityEventAsync(
        ulong? userId,
        string? emailSnapshot,
        string eventType,
        string result,
        string? failureReasonCode = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? loginPortal = null,
        string? detailText = null,
        CancellationToken cancellationToken = default);
}

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Writes audit trails for authentication events. Each method persists
/// immediately so the record survives even when the surrounding request fails
/// (e.g. a failed-login row must be written before the 401 is thrown).
/// </summary>
public interface ISecurityAuditService
{
    Task WriteLoginLogAsync(
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
        CancellationToken cancellationToken = default);

    Task WriteSecurityEventAsync(
        string? userId,
        string? email,
        string eventType,
        string severity,
        string? ipAddress,
        string? userAgent,
        string? metadata = null,
        CancellationToken cancellationToken = default);
}

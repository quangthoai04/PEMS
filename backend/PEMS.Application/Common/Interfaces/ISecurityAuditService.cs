namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Writes audit trails for authentication events. Each method persists
/// immediately so the record survives even when the surrounding request fails
/// (e.g. a failed-login row must be written before the 401 is thrown).
/// </summary>
public interface ISecurityAuditService
{
    Task WriteLoginLogAsync(
        ulong? userId,
        string email,
        string loginPortal,
        ulong? selectedCampusId,
        string? providerType,
        string status,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        ulong? sessionId,
        CancellationToken cancellationToken = default);

    Task WriteSecurityEventAsync(
        ulong? userId,
        string? email,
        string eventType,
        string severity,
        string? ipAddress,
        string? userAgent,
        string? metadata = null,
        CancellationToken cancellationToken = default);
}

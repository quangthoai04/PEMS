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
        string? emailSnapshot,
        string eventType,
        string result,
        string? failureReasonCode = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? loginPortal = null,
        ulong? selectedCampusId = null,
        string? providerType = null,
        ulong? sessionId = null,
        string? detailText = null,
        CancellationToken cancellationToken = default);
}

using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Admin.Queries.GetAdminAuditLogs;

/// <summary>GET /api/admin/audit-logs — paginated audit_logs with actor/action/entity/time filters.</summary>
public sealed class GetAdminAuditLogsQuery : IRequest<PaginatedResult<AdminAuditLogItemDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    /// <summary>Actor email/full-name keyword.</summary>
    public string? Keyword { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public ulong? CampusId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class AdminAuditLogItemDto
{
    public ulong AuditLogId { get; set; }
    public ulong? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public ulong? EntityId { get; set; }
    public ulong? CampusId { get; set; }
    public string? CampusName { get; set; }
    public string? IpAddress { get; set; }
    public string? RequestId { get; set; }
    public int ChangeCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

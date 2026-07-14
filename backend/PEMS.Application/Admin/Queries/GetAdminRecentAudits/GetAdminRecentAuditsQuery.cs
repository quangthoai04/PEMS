using MediatR;

namespace PEMS.Application.Admin.Queries.GetAdminRecentAudits;

/// <summary>GET /api/admin/dashboard/recent-audits — latest audit-log entries for the dashboard.</summary>
public sealed class GetAdminRecentAuditsQuery : IRequest<List<AdminRecentAuditItemDto>>
{
    public int Limit { get; set; } = 10;
}

public sealed class AdminRecentAuditItemDto
{
    public ulong AuditLogId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public ulong? EntityId { get; set; }
    public string? CampusName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

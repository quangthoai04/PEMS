using MediatR;

namespace PEMS.Application.Admin.Queries.GetAdminAuditLogDetail;

/// <summary>
/// GET /api/admin/audit-logs/{id} — one audit log with its before/after changes.
/// Sensitive values (password/token/credential/secret/cookie/refresh…) are masked.
/// </summary>
public sealed class GetAdminAuditLogDetailQuery : IRequest<AdminAuditLogDetailDto>
{
    public GetAdminAuditLogDetailQuery(ulong auditLogId) => AuditLogId = auditLogId;
    public ulong AuditLogId { get; }
}

public sealed class AdminAuditLogDetailDto
{
    public ulong AuditLogId { get; set; }
    public ulong? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorRoleCode { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public ulong? EntityId { get; set; }
    public ulong? CampusId { get; set; }
    public string? CampusName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AdminAuditLogChangeDto> Changes { get; set; } = new();
}

public sealed class AdminAuditLogChangeDto
{
    public ulong AuditLogChangeId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    /// <summary>True when the value was replaced by a mask because the field is sensitive.</summary>
    public bool IsMasked { get; set; }
}

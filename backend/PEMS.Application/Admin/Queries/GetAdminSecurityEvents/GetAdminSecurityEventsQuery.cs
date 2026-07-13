using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Admin.Queries.GetAdminSecurityEvents;

/// <summary>GET /api/admin/security-events — paginated security_events with severity/result/portal/provider/IP/user/time filters.</summary>
public sealed class GetAdminSecurityEventsQuery : IRequest<PaginatedResult<AdminSecurityEventDetailDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    /// <summary>Email keyword (snapshot or linked user).</summary>
    public string? Keyword { get; set; }
    public string? Severity { get; set; }
    public string? Result { get; set; }
    public string? EventType { get; set; }
    public string? Portal { get; set; }
    public string? Provider { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class AdminSecurityEventDetailDto
{
    public ulong SecurityEventId { get; set; }
    public ulong? UserId { get; set; }
    public string? Email { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? FailureReasonCode { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? LoginPortal { get; set; }
    public string? ProviderType { get; set; }
    public ulong? SessionId { get; set; }
    public string? DetailText { get; set; }
    public DateTime CreatedAt { get; set; }
}

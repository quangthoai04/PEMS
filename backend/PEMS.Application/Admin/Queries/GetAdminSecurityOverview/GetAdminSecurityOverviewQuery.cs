using MediatR;

namespace PEMS.Application.Admin.Queries.GetAdminSecurityOverview;

/// <summary>
/// GET /api/admin/dashboard/security — severity counters (7 days) + the most recent
/// HIGH/CRITICAL security events for the dashboard's security panel.
/// </summary>
public sealed class GetAdminSecurityOverviewQuery : IRequest<AdminSecurityOverviewDto>
{
}

public sealed class AdminSecurityOverviewDto
{
    public int Low { get; set; }
    public int Medium { get; set; }
    public int High { get; set; }
    public int Critical { get; set; }
    public List<AdminSecurityEventItemDto> RecentHighSeverity { get; set; } = new();
}

public sealed class AdminSecurityEventItemDto
{
    public ulong SecurityEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? IpAddress { get; set; }
    public string? LoginPortal { get; set; }
    public string? ProviderType { get; set; }
    public DateTime CreatedAt { get; set; }
}

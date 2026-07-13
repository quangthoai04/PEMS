using MediatR;

namespace PEMS.Application.Admin.Queries.GetAdminIntegrationsOverview;

/// <summary>
/// GET /api/admin/dashboard/integrations — daily API request SUCCESS/FAILED counts and
/// average response time for the dashboard chart. <see cref="Days"/> clamped to 1..90.
/// </summary>
public sealed class GetAdminIntegrationsOverviewQuery : IRequest<List<AdminApiRequestActivityPointDto>>
{
    public int Days { get; set; } = 7;
}

public sealed class AdminApiRequestActivityPointDto
{
    /// <summary>Vietnam calendar date (yyyy-MM-dd).</summary>
    public string Date { get; set; } = string.Empty;
    public int Success { get; set; }
    public int Failed { get; set; }
    public int? AvgResponseTimeMs { get; set; }
}

using System;

namespace PEMS.Application.Reports.Queries.ViewDashboardStatistics;

public sealed class ViewDashboardStatisticsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
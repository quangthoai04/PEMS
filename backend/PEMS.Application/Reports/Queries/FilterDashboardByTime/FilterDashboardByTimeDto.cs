using System;

namespace PEMS.Application.Reports.Queries.FilterDashboardByTime;

public sealed class FilterDashboardByTimeDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
using System;

namespace PEMS.Application.ApiIntegrations.Queries.ViewAPILogs;

public sealed class ViewAPILogsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
using System;

namespace PEMS.Application.ApiIntegrations.Queries.ViewAPIConfiguration;

public sealed class ViewAPIConfigurationDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
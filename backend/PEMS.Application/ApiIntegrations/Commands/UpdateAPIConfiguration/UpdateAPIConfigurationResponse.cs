using System;

namespace PEMS.Application.ApiIntegrations.Commands.UpdateAPIConfiguration;

public sealed class UpdateAPIConfigurationResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
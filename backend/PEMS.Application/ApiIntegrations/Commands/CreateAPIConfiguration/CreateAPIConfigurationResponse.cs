using System;

namespace PEMS.Application.ApiIntegrations.Commands.CreateAPIConfiguration;

public sealed class CreateAPIConfigurationResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
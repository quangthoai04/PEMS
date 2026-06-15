using System;

namespace PEMS.Application.ApiIntegrations.Commands.DeleteAPIConfiguration;

public sealed class DeleteAPIConfigurationResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
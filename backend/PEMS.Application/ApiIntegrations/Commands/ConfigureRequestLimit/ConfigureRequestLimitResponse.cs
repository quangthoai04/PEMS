using System;

namespace PEMS.Application.ApiIntegrations.Commands.ConfigureRequestLimit;

public sealed class ConfigureRequestLimitResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
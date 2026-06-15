using System;

namespace PEMS.Application.ApiIntegrations.Commands.ManageAPIStatus;

public sealed class ManageAPIStatusResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
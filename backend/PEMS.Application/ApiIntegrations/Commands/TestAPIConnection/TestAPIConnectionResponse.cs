using System;

namespace PEMS.Application.ApiIntegrations.Commands.TestAPIConnection;

public sealed class TestAPIConnectionResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
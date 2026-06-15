using System;

namespace PEMS.Application.Campuses.Commands.ManageCampusStatus;

public sealed class ManageCampusStatusResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
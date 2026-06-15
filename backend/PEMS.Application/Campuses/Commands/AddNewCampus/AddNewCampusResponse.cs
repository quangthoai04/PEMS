using System;

namespace PEMS.Application.Campuses.Commands.AddNewCampus;

public sealed class AddNewCampusResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
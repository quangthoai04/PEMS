using System;

namespace PEMS.Application.Departments.Commands.RemovePersonnel;

public sealed class RemovePersonnelResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
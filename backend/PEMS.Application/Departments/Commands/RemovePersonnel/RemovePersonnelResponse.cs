using System;

namespace PEMS.Application.Departments.Commands.RemovePersonnel;

public sealed class RemovePersonnelResponse
{
    public Guid? Id { get; init; }
    public string Status { get; set; } = "Scaffolded";
    public string Message { get; set; } = "Use case scaffolded. Business logic is not implemented yet.";
    public bool Success { get; set; }
}
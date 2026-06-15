using System;

namespace PEMS.Application.Departments.Commands.AddNewDepartment;

public sealed class AddNewDepartmentResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
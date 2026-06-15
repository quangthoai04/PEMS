using System;

namespace PEMS.Application.Departments.Queries.ViewDepartmentDetails;

public sealed class ViewDepartmentDetailsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
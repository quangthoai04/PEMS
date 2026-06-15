using System;

namespace PEMS.Application.Departments.Queries.ViewDepartmentList;

public sealed class ViewDepartmentListDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
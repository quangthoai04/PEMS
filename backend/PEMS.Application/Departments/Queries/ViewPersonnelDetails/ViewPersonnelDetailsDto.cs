using System;

namespace PEMS.Application.Departments.Queries.ViewPersonnelDetails;

public sealed class ViewPersonnelDetailsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
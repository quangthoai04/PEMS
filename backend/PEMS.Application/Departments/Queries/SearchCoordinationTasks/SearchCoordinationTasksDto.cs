using System;

namespace PEMS.Application.Departments.Queries.SearchCoordinationTasks;

public sealed class SearchCoordinationTasksDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
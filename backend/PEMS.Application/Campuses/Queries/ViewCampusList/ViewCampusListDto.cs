using System;

namespace PEMS.Application.Campuses.Queries.ViewCampusList;

public sealed class ViewCampusListDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
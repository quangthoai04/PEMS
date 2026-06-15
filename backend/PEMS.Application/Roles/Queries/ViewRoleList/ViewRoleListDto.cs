using System;

namespace PEMS.Application.Roles.Queries.ViewRoleList;

public sealed class ViewRoleListDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
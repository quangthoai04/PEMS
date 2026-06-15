using System;

namespace PEMS.Application.Roles.Commands.DisableAndDeleteRole;

public sealed class DisableAndDeleteRoleResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
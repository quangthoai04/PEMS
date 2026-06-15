using System;

namespace PEMS.Application.Roles.Commands.ConfigureRolePermissions;

public sealed class ConfigureRolePermissionsResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
using System;

namespace PEMS.Application.Roles.Commands.UpdateRoleDetails;

public sealed class UpdateRoleDetailsResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
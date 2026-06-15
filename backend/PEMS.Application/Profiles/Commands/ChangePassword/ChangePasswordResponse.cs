using System;

namespace PEMS.Application.Profiles.Commands.ChangePassword;

public sealed class ChangePasswordResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
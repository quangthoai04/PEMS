using System;

namespace PEMS.Application.Profiles.Commands.UpdateProfile;

public sealed class UpdateProfileResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
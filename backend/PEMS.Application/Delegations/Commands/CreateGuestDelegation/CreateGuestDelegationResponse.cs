using System;

namespace PEMS.Application.Delegations.Commands.CreateGuestDelegation;

public sealed class CreateGuestDelegationResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
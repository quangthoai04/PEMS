using System;

namespace PEMS.Application.Delegations.Commands.UpdateGuestDelegation;

public sealed class UpdateGuestDelegationResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
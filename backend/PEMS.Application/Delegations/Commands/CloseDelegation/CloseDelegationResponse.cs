using System;

namespace PEMS.Application.Delegations.Commands.CloseDelegation;

public sealed class CloseDelegationResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
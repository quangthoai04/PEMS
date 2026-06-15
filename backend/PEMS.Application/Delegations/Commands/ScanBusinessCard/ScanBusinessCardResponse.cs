using System;

namespace PEMS.Application.Delegations.Commands.ScanBusinessCard;

public sealed class ScanBusinessCardResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
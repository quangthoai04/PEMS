using System;

namespace PEMS.Application.Delegations.Commands.ConfirmParticipation;

public sealed class ConfirmParticipationResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
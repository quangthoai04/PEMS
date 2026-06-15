using System;

namespace PEMS.Application.Delegations.Commands.ApproveCrossCampusRequest;

public sealed class ApproveCrossCampusRequestResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
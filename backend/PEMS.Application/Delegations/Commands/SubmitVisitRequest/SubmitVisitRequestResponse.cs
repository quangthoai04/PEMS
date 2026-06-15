using System;

namespace PEMS.Application.Delegations.Commands.SubmitVisitRequest;

public sealed class SubmitVisitRequestResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
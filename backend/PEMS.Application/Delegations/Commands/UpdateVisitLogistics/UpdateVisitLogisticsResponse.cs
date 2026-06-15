using System;

namespace PEMS.Application.Delegations.Commands.UpdateVisitLogistics;

public sealed class UpdateVisitLogisticsResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
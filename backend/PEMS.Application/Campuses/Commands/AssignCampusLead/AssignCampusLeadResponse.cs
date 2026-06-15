using System;

namespace PEMS.Application.Campuses.Commands.AssignCampusLead;

public sealed class AssignCampusLeadResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
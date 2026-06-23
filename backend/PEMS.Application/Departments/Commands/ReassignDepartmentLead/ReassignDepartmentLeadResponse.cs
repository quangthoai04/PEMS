using System;

namespace PEMS.Application.Departments.Commands.ReassignDepartmentLead;

public sealed class ReassignDepartmentLeadResponse
{
    public Guid? Id { get; init; }
    public string Status { get; set; } = "Scaffolded";
    public string Message { get; set; } = "Use case scaffolded. Business logic is not implemented yet.";
    public bool Success { get; set; }
}
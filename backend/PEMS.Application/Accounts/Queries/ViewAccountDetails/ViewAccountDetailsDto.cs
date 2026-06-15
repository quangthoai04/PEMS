using System;

namespace PEMS.Application.Accounts.Queries.ViewAccountDetails;

public sealed class ViewAccountDetailsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
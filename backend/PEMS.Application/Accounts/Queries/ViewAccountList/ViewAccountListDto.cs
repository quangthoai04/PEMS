using System;

namespace PEMS.Application.Accounts.Queries.ViewAccountList;

public sealed class ViewAccountListDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
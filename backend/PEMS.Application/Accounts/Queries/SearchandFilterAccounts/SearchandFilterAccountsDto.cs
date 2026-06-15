using System;

namespace PEMS.Application.Accounts.Queries.SearchandFilterAccounts;

public sealed class SearchandFilterAccountsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
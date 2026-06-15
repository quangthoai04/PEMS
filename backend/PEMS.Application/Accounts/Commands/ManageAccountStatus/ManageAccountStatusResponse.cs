using System;

namespace PEMS.Application.Accounts.Commands.ManageAccountStatus;

public sealed class ManageAccountStatusResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
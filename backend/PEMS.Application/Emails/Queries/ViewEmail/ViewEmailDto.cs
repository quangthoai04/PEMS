using System;

namespace PEMS.Application.Emails.Queries.ViewEmail;

public sealed class ViewEmailDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
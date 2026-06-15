using System;

namespace PEMS.Application.PublicContent.Queries.ViewFAQ;

public sealed class ViewFAQDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
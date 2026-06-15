using System;

namespace PEMS.Application.Faqs.Queries.ViewListFAQ;

public sealed class ViewListFAQDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
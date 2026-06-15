using System;

namespace PEMS.Application.Faqs.Queries.SearchFAQ;

public sealed class SearchFAQDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
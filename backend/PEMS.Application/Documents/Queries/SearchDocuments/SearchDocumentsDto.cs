using System;

namespace PEMS.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
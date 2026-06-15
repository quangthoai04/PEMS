using System;

namespace PEMS.Application.Documents.Queries.ViewDocumentList;

public sealed class ViewDocumentListDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
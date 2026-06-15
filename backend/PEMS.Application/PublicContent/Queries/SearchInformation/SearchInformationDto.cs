using System;

namespace PEMS.Application.PublicContent.Queries.SearchInformation;

public sealed class SearchInformationDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
using System;

namespace PEMS.Application.PublicContent.Queries.ViewHomepage;

public sealed class ViewHomepageDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
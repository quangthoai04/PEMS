using System;

namespace PEMS.Application.PublicContent.Queries.ViewNews;

public sealed class ViewNewsDto
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
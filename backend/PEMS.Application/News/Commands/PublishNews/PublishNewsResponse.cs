using System;

namespace PEMS.Application.News.Commands.PublishNews;

public sealed class PublishNewsResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
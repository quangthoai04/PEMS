using System;

namespace PEMS.Application.Delegations.Commands.CreateNewsArticle;

public sealed class CreateNewsArticleResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
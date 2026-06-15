using System;

namespace PEMS.Application.News.Commands.ManageNewsVisibility;

public sealed class ManageNewsVisibilityResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
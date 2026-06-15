using System;

namespace PEMS.Application.Faqs.Commands.ChangeFAQVisibility;

public sealed class ChangeFAQVisibilityResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
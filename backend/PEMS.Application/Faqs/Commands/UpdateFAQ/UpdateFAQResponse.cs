using System;

namespace PEMS.Application.Faqs.Commands.UpdateFAQ;

public sealed class UpdateFAQResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
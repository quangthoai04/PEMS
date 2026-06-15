using System;

namespace PEMS.Application.Emails.Commands.UpdateEmailTemplate;

public sealed class UpdateEmailTemplateResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
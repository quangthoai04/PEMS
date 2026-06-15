using System;

namespace PEMS.Application.Emails.Commands.CreateEmailTemplate;

public sealed class CreateEmailTemplateResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
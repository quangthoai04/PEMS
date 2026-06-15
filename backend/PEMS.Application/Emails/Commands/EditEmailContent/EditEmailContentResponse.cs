using System;

namespace PEMS.Application.Emails.Commands.EditEmailContent;

public sealed class EditEmailContentResponse
{
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
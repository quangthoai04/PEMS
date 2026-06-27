using System;

namespace PEMS.Application.Emails.Commands.ReplytoEmail;

public sealed class ReplytoEmailResponse
{
    public bool Success { get; init; }
    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; init; } = "Use case scaffolded. Business logic is not implemented yet.";
}
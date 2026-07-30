using System;

namespace PEMS.Application.Emails.Commands.ReplytoEmail;

public sealed class ReplytoEmailResponse : PEMS.Application.Emails.Idempotency.IEmailSendResult
{
    /// <summary>
    /// Settable rather than init-only: a replayed reply is reconstructed by the idempotency behaviour
    /// from the stored reservation, which cannot use an object initialiser.
    /// </summary>
    public bool Success { get; set; }

    public Guid? Id { get; init; }
    public string Status { get; init; } = "Scaffolded";
    public string Message { get; set; } = "Use case scaffolded. Business logic is not implemented yet.";
}
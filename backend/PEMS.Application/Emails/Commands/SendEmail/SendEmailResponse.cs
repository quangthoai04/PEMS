using System;

namespace PEMS.Application.Emails.Commands.SendEmail;

public sealed class SendEmailResponse : PEMS.Application.Emails.Idempotency.IEmailSendResult
{
    public Guid? Id { get; init; }
    public ulong? SentEmailId { get; init; }

    /// <summary>
    /// SENT (the provider accepted the message) | FAILED (it rejected it) | QUEUED (sending is switched
    /// off in this environment, so nothing was handed to a provider at all). Never DELIVERED: PEMS has no
    /// delivery webhook and cannot know that a mailbox received anything.
    /// </summary>
    public string Status { get; init; } = "QUEUED";

    /// <summary>
    /// True only for a real provider acceptance — not for a skipped or failed send.
    ///
    /// <para>
    /// Settable rather than init-only because a replayed send is reconstructed by the idempotency
    /// behaviour from the stored reservation, which cannot use an object initialiser.
    /// </para>
    /// </summary>
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

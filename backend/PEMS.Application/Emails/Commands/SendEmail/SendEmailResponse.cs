using System;

namespace PEMS.Application.Emails.Commands.SendEmail;

public sealed class SendEmailResponse
{
    public Guid? Id { get; init; }
    public ulong? SentEmailId { get; init; }

    /// <summary>
    /// SENT (the provider accepted the message) | FAILED (it rejected it) | QUEUED (sending is switched
    /// off in this environment, so nothing was handed to a provider at all). Never DELIVERED: PEMS has no
    /// delivery webhook and cannot know that a mailbox received anything.
    /// </summary>
    public string Status { get; init; } = "QUEUED";

    /// <summary>True only for a real provider acceptance — not for a skipped or failed send.</summary>
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

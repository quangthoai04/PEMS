using System;

namespace PEMS.Application.Emails.Commands.SendEmail;

public sealed class SendEmailResponse
{
    public Guid? Id { get; init; }
    public ulong? SentEmailId { get; init; }
    public string Status { get; init; } = "QUEUED";
    /// <summary>True only when every recipient was delivered successfully.</summary>
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

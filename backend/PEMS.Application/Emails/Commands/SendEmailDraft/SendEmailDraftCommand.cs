using MediatR;

namespace PEMS.Application.Emails.Commands.SendEmailDraft;

/// <summary>
/// Sends a DRAFT: produces a sent_emails row (+ recipients + attachments), dispatches via the email
/// service, then marks the draft SENT and links it to the produced sent_email. Recipients and
/// attachment scope are re-validated at send time. Only the owner may send, only from DRAFT status.
/// </summary>
public sealed record SendEmailDraftCommand(ulong EmailDraftId) : IRequest<SendEmailDraftResponse>;

public sealed class SendEmailDraftResponse
{
    public ulong EmailDraftId { get; init; }
    public ulong SentEmailId { get; init; }
    /// <summary>SENT | PARTIAL_FAILED | FAILED — overall send outcome.</summary>
    public string Status { get; init; } = "SENT";
    /// <summary>True only when every recipient was delivered successfully (Status == SENT).</summary>
    public bool Success { get; init; }
    public string DraftStatus { get; init; } = "SENT";
    public string Message { get; init; } = string.Empty;
}

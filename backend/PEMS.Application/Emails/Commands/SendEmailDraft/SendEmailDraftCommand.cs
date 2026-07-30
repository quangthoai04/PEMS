using MediatR;

namespace PEMS.Application.Emails.Commands.SendEmailDraft;

/// <summary>
/// Sends a DRAFT: produces one sent_emails row (+ recipients + attachments), dispatches ONE MIME message
/// to the whole TO/CC/BCC envelope, then links the draft to the message it became. Recipients, content
/// and attachment scope are all re-validated at send time. Only the owner may send, only from DRAFT
/// status, and the draft is claimed atomically so two simultaneous clicks cannot both send.
/// </summary>
public sealed record SendEmailDraftCommand(ulong EmailDraftId) : IRequest<SendEmailDraftResponse>;

public sealed class SendEmailDraftResponse
{
    public ulong EmailDraftId { get; init; }
    public ulong SentEmailId { get; init; }

    /// <summary>
    /// SENT (the provider accepted it) | FAILED (it rejected it) | QUEUED (sending is switched off in this
    /// environment). There is no partial outcome any more: one envelope is one message, so it has one
    /// result — the old PARTIAL_FAILED existed only because the handler sent a separate email per address.
    /// </summary>
    public string Status { get; init; } = "SENT";

    /// <summary>True only for a real provider acceptance — not for a skipped or failed send.</summary>
    public bool Success { get; init; }
    public string DraftStatus { get; init; } = "SENT";
    public string Message { get; init; } = string.Empty;
}

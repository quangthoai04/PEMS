using MediatR;

namespace PEMS.Application.Emails.Commands.DiscardEmailDraft;

/// <summary>
/// Soft-discards a draft (status → DISCARDED, never hard-deleted). Only the owner may discard, and
/// only while the draft is still in DRAFT status.
/// </summary>
public sealed record DiscardEmailDraftCommand(ulong EmailDraftId) : IRequest<DiscardEmailDraftResponse>;

public sealed class DiscardEmailDraftResponse
{
    public ulong EmailDraftId { get; init; }
    public string Status { get; init; } = "DISCARDED";
}

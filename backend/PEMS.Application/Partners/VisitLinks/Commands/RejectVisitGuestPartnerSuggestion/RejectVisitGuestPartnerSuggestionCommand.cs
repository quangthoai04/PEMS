using MediatR;

namespace PEMS.Application.Partners.VisitLinks.Commands.RejectVisitGuestPartnerSuggestion;

/// <summary>
/// POST /api/visit-instances/{visitInstanceId}/partner-links/{linkId}/reject-suggestion —
/// marks a SUGGESTED link REJECTED so it stops showing as an active badge.
/// </summary>
public sealed record RejectVisitGuestPartnerSuggestionCommand(ulong VisitInstanceId, ulong LinkId)
    : IRequest<RejectVisitGuestPartnerSuggestionResponse>;

public sealed class RejectVisitGuestPartnerSuggestionResponse
{
    public ulong LinkId { get; set; }
    public string MatchStatus { get; set; } = "REJECTED";
}

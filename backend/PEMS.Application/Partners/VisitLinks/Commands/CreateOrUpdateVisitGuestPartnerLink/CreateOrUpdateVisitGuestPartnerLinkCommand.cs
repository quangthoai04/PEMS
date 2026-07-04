using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.VisitLinks.Commands.CreateOrUpdateVisitGuestPartnerLink;

/// <summary>
/// POST /api/visit-instances/{visitInstanceId}/partner-links (LinkId null → create)
/// PUT  /api/visit-instances/{visitInstanceId}/partner-links/{linkId} (LinkId set → update)
/// At least one of GuestMemberId / MinuteParticipantId is required.
/// </summary>
public sealed class CreateOrUpdateVisitGuestPartnerLinkCommand : IRequest<VisitGuestPartnerLinkDto>
{
    public ulong VisitInstanceId { get; set; }
    public ulong? LinkId { get; set; }
    public ulong? GuestMemberId { get; set; }
    public ulong? MinuteParticipantId { get; set; }
    public ulong PartnerId { get; set; }
    public ulong? PartnerContactId { get; set; }
    /// <summary>MANUAL (default) | CREATED_FROM_GUEST | BUSINESS_CARD_OCR ...</summary>
    public string? MatchSource { get; set; }
    /// <summary>SUGGESTED | CONFIRMED (default CONFIRMED for manual link).</summary>
    public string? MatchStatus { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? Note { get; set; }
}

using MediatR;

namespace PEMS.Application.Partners.Commands.CreatePartnerFromGuest;

/// <summary>
/// POST /api/visit-instances/{visitInstanceId}/partner-links/create-partner —
/// creates a PENDING_APPROVAL partner prefilled from a visit guest / minute participant
/// and links that person to the new partner (match_source CREATED_FROM_GUEST).
/// </summary>
public sealed class CreatePartnerFromGuestCommand : IRequest<CreatePartnerFromGuestResponse>
{
    public ulong VisitInstanceId { get; set; }
    public ulong? GuestMemberId { get; set; }
    public ulong? MinuteParticipantId { get; set; }

    /// <summary>Optional overrides — defaults come from the guest snapshot.</summary>
    public string? PartnerName { get; set; }
    public string? PartnerCode { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? PartnerType { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}

public sealed class CreatePartnerFromGuestResponse
{
    public ulong PartnerId { get; set; }
    public string ProfileStatus { get; set; } = string.Empty;
    public ulong LinkId { get; set; }
    public ulong? InitialContactId { get; set; }
}

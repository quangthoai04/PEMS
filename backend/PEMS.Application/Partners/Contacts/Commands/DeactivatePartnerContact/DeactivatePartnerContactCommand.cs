using MediatR;

namespace PEMS.Application.Partners.Contacts.Commands.DeactivatePartnerContact;

/// <summary>
/// DELETE /api/partners/{partnerId}/contacts/{contactId} — soft delete only
/// (status → INACTIVE); contacts with business links are never hard-deleted.
/// </summary>
public sealed record DeactivatePartnerContactCommand(ulong PartnerId, ulong ContactId)
    : IRequest<DeactivatePartnerContactResponse>;

public sealed class DeactivatePartnerContactResponse
{
    public ulong ContactId { get; set; }
    public string Status { get; set; } = "INACTIVE";
}

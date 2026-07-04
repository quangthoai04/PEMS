using MediatR;

namespace PEMS.Application.Partners.Contacts.Commands.SetPrimaryPartnerContact;

/// <summary>POST /api/partners/{partnerId}/contacts/{contactId}/set-primary</summary>
public sealed record SetPrimaryPartnerContactCommand(ulong PartnerId, ulong ContactId)
    : IRequest<SetPrimaryPartnerContactResponse>;

public sealed class SetPrimaryPartnerContactResponse
{
    public ulong ContactId { get; set; }
    public bool IsPrimary { get; set; }
}

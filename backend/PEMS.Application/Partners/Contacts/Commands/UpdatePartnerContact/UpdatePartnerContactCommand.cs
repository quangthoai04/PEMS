using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Contacts.Commands.UpdatePartnerContact;

/// <summary>PUT /api/partners/{partnerId}/contacts/{contactId}</summary>
public sealed class UpdatePartnerContactCommand : IRequest<PartnerContactDto>
{
    public ulong PartnerId { get; set; }
    public ulong ContactId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? Note { get; set; }
}

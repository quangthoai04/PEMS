using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Contacts.Commands.CreatePartnerContact;

/// <summary>POST /api/partners/{partnerId}/contacts</summary>
public sealed class CreatePartnerContactCommand : IRequest<PartnerContactDto>
{
    public ulong PartnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? Note { get; set; }
    public ulong? AvatarFileId { get; set; }
    public bool IsPrimary { get; set; }
}

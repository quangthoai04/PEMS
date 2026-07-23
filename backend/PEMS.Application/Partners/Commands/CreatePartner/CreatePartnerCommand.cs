using MediatR;

namespace PEMS.Application.Partners.Commands.CreatePartner;

/// <summary>
/// POST /api/partners — IC Staff / Staff Leader creates a partner in PENDING_APPROVAL.
/// owner_campus_id is NEVER accepted from the client: the backend sets
/// owner_campus_id = currentUser.primary_campus_id.
/// </summary>
public sealed class CreatePartnerCommand : IRequest<CreatePartnerResponse>
{
    public string? PartnerCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? PartnerType { get; set; }
    /// <summary>PRIVATE | INTERNAL (PUBLIC is rejected while not APPROVED).</summary>
    public string? Visibility { get; set; }
    public ulong? LogoFileId { get; set; }
    public ulong? CoverFileId { get; set; }
    /// <summary>MANUAL | FROM_GUEST | BUSINESS_CARD_OCR — informational source of the create.</summary>
    public string? Source { get; set; }
    public InitialContactPayload? InitialContact { get; set; }

    /// <summary>
    /// English content, only set once the admin has opened the "EN" panel (whether the text is
    /// the untouched machine-translation preview or hand-edited afterward). Null/omitted when the
    /// EN panel was never opened — the backend then auto-translates once and stores the result.
    /// </summary>
    public string? EnglishName { get; set; }
    public string? EnglishShortName { get; set; }
    public string? EnglishDescription { get; set; }
    public string? EnglishAddress { get; set; }

    public sealed class InitialContactPayload
    {
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? JobTitle { get; set; }
        public string? DepartmentName { get; set; }
        public string? Note { get; set; }
    }
}

public sealed class CreatePartnerResponse
{
    public ulong PartnerId { get; set; }
    public string ProfileStatus { get; set; } = string.Empty;
    public ulong OwnerCampusId { get; set; }
    public ulong? InitialContactId { get; set; }
    public string? EnglishName { get; set; }
    public string? EnglishShortName { get; set; }
    public string? EnglishDescription { get; set; }
    public string? EnglishAddress { get; set; }
}

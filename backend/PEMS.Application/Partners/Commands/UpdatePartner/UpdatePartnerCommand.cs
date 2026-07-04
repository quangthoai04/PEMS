using MediatR;

namespace PEMS.Application.Partners.Commands.UpdatePartner;

/// <summary>
/// PUT /api/partners/{partnerId}. owner_campus_id can never be changed from the client.
/// Updating a REJECTED profile resubmits it (back to PENDING_APPROVAL, review fields cleared).
/// </summary>
public sealed class UpdatePartnerCommand : IRequest<UpdatePartnerResponse>
{
    public ulong PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? PartnerType { get; set; }
    public string? CooperationStatus { get; set; }
    public string? Visibility { get; set; }
    public ulong? LogoFileId { get; set; }
    public ulong? CoverFileId { get; set; }
}

public sealed class UpdatePartnerResponse
{
    public ulong PartnerId { get; set; }
    public string ProfileStatus { get; set; } = string.Empty;
}

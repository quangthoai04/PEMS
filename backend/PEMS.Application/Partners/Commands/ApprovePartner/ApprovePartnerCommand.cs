using MediatR;

namespace PEMS.Application.Partners.Commands.ApprovePartner;

/// <summary>POST /api/partners/{partnerId}/approve — Staff Leader of the owner campus only.</summary>
public sealed class ApprovePartnerCommand : IRequest<ApprovePartnerResponse>
{
    public ulong PartnerId { get; set; }
    public string? ReviewNote { get; set; }
    /// <summary>Optional: make the approved profile PUBLIC right away.</summary>
    public bool MakePublic { get; set; }
}

public sealed class ApprovePartnerResponse
{
    public ulong PartnerId { get; set; }
    public string ProfileStatus { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
}

using MediatR;

namespace PEMS.Application.Partners.Commands.RejectPartner;

/// <summary>POST /api/partners/{partnerId}/reject — reason is mandatory.</summary>
public sealed class RejectPartnerCommand : IRequest<RejectPartnerResponse>
{
    public ulong PartnerId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class RejectPartnerResponse
{
    public ulong PartnerId { get; set; }
    public string ProfileStatus { get; set; } = string.Empty;
}

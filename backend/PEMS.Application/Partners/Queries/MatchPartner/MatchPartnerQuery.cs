using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.MatchPartner;

/// <summary>GET /api/partners/match?organization=...&amp;email=...</summary>
public sealed class MatchPartnerQuery : IRequest<PartnerMatchDto>
{
    public string? Organization { get; set; }
    public string? Email { get; set; }
}

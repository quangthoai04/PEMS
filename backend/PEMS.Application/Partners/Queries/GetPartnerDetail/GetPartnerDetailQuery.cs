using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPartnerDetail;

/// <summary>GET /api/partners/{partnerId}</summary>
public sealed record GetPartnerDetailQuery(ulong PartnerId) : IRequest<PartnerDetailDto>;

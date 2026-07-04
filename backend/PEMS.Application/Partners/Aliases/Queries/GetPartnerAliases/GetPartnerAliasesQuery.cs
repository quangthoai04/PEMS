using System.Collections.Generic;
using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Aliases.Queries.GetPartnerAliases;

/// <summary>GET /api/partners/{partnerId}/aliases</summary>
public sealed record GetPartnerAliasesQuery(ulong PartnerId) : IRequest<List<PartnerAliasDto>>;

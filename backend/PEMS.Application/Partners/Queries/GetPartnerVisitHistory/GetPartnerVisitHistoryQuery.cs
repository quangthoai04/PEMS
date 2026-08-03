using System.Collections.Generic;
using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPartnerVisitHistory;

public sealed record GetPartnerVisitHistoryQuery(ulong PartnerId)
    : IRequest<List<PartnerVisitHistoryDto>>;

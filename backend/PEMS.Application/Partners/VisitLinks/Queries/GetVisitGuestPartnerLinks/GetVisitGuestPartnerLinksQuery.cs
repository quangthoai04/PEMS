using System.Collections.Generic;
using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.VisitLinks.Queries.GetVisitGuestPartnerLinks;

/// <summary>GET /api/visit-instances/{visitInstanceId}/partner-links</summary>
public sealed record GetVisitGuestPartnerLinksQuery(ulong VisitInstanceId)
    : IRequest<List<VisitGuestPartnerLinkDto>>;

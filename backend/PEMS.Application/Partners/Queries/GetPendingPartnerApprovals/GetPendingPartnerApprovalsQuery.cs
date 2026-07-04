using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPendingPartnerApprovals;

/// <summary>GET /api/partners/pending-approvals — PENDING_APPROVAL rows of the leader's own campus.</summary>
public sealed class GetPendingPartnerApprovalsQuery : IRequest<PartnerListResponse>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

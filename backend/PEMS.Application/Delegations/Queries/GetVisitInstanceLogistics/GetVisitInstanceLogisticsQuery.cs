using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceLogistics;

/// <summary>
/// Lists the logistics/resource requests of a campus instance for the VisitProcess "Chuẩn bị chi
/// tiết" panel. Visible to the instance host, the campus Staff Leader, and HO (read-only). Status
/// labels are derived on the frontend (LOGISTICS_STATUS_META) — the backend returns raw enum values.
/// </summary>
public sealed record GetVisitInstanceLogisticsQuery(ulong VisitInstanceId)
    : IRequest<GetVisitInstanceLogisticsResponse>;

public sealed class GetVisitInstanceLogisticsResponse
{
    public List<VisitInstanceLogisticsItemDto> Items { get; set; } = new();
}

public sealed class VisitInstanceLogisticsItemDto
{
    public ulong LogisticsItemId { get; set; }
    public string ItemType { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public int? Quantity { get; set; }
    public string Status { get; set; } = default!;     // REQUESTED | ASSIGNED | ACCEPTED | ...
    public string Priority { get; set; } = default!;
    public string CoordinationMode { get; set; } = default!;  // SYSTEM_REQUEST | OFFLINE_COORDINATED
    public string? OfflineCoordinationNote { get; set; }
    public ulong? RequestedToDepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? RequestedAt { get; set; }           // "yyyy-MM-ddTHH:mm:ss" wall-clock
    public ulong? RequestedBy { get; set; }
    public string? RequestedByName { get; set; }
    public string? UsageStartAt { get; set; }
    public string? UsageEndAt { get; set; }
    public string? DueAt { get; set; }
    public string? CompletedAt { get; set; }
    public ulong? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public string? AssigneeResponseNote { get; set; }
    public string? DecisionNote { get; set; }          // close reason for REJECTED / CANCELLED / DECLINED

    // ── Change-proposal (Department negotiates the Host's PLANNED quantity/time/content) ──
    // Quantity above is the PLANNED/requested figure; the FINAL ("chốt") quantity is computed:
    // ProposedQuantity when ProposalResponse == "ACCEPTED", otherwise Quantity. No actual_quantity
    // column exists, so the original Quantity is never overwritten.
    public int? ProposedQuantity { get; set; }
    public string? ProposedUsageStartAt { get; set; }
    public string? ProposedUsageEndAt { get; set; }
    public string? ProposedDescription { get; set; }
    public string? ProposalNote { get; set; }
    public string? ProposalResponse { get; set; }      // ACCEPTED | REJECTED | null
    public string? ProposalResponseNote { get; set; }

    // ── Borrow/return handover signatures (visit_logistics_item_handovers) ──
    public List<LogisticsHandoverDto> Handovers { get; set; } = new();
}

public sealed class LogisticsHandoverDto
{
    public string HandoverType { get; set; } = default!;   // BORROW | RETURN
    public string? BorrowerSignedByName { get; set; }
    public string? BorrowerSignedAt { get; set; }
    public string? ProviderSignedByName { get; set; }
    public string? ProviderSignedAt { get; set; }
    public string? ItemCondition { get; set; }             // GOOD | DAMAGED | MISSING | OTHER
    public string? ConditionNote { get; set; }
}

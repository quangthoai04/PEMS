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
    public ulong? RequestedToDepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? RequestedAt { get; set; }           // "yyyy-MM-ddTHH:mm:ss" wall-clock
    public string? UsageStartAt { get; set; }
    public string? UsageEndAt { get; set; }
    public string? DueAt { get; set; }
    public ulong? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
}

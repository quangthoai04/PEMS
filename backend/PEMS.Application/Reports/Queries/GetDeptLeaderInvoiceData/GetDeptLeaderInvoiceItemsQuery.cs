using MediatR;
using System.Collections.Generic;

namespace PEMS.Application.Reports.Queries.GetDeptLeaderInvoiceData;

/// <summary>
/// Logistics items the host asked the current leader's department to prepare for one visit.
/// Quantity always comes from the database — the leader only enters unit prices on the UI.
/// </summary>
public sealed class GetDeptLeaderInvoiceItemsQuery : IRequest<List<DeptLeaderInvoiceItemDto>>
{
    public ulong VisitInstanceId { get; set; }
}

public class DeptLeaderInvoiceItemDto
{
    public ulong LogisticsItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string ItemTypeLabelVi { get; set; } = string.Empty;
    public int Quantity { get; set; }
    /// <summary>Schema has no unit column; kept for UI symmetry.</summary>
    public string? Unit { get; set; }
}

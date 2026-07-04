using MediatR;
using System;
using System.Collections.Generic;

namespace PEMS.Application.Reports.Queries.GetDeptLeaderInvoiceData;

/// <summary>
/// Visits (campus instances) that have at least one logistics item requested to the
/// current Department Leader's department — the selectable list for the invoice tab.
/// </summary>
public sealed class GetDeptLeaderInvoiceVisitsQuery : IRequest<List<DeptLeaderInvoiceVisitDto>>
{
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class DeptLeaderInvoiceVisitDto
{
    public ulong VisitInstanceId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string DelegationName { get; set; } = string.Empty;
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
}

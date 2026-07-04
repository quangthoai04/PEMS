using MediatR;
using System;
using System.Collections.Generic;

namespace PEMS.Application.Reports.Commands.ExportDeptLeaderInvoice;

/// <summary>
/// Generates (download-only, nothing persisted) a professional PDF invoice for the
/// logistics items the host asked the leader's department to prepare for one visit.
/// Quantity comes from the database; the unit price is entered by the Department Leader.
/// </summary>
public class ExportDeptLeaderInvoiceCommand : IRequest<ExportDeptLeaderInvoiceResult>
{
    public ulong VisitInstanceId { get; set; }
    public string? InvoiceTitle { get; set; }
    public string? InvoiceNote { get; set; }
    public List<ExportDeptLeaderInvoiceItem> Items { get; set; } = new();
}

public class ExportDeptLeaderInvoiceItem
{
    public ulong LogisticsItemId { get; set; }
    public string? ItemName { get; set; }
    public string? ItemType { get; set; }
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Note { get; set; }
}

public class ExportDeptLeaderInvoiceResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/pdf";
    public string FileName { get; set; } = null!;
}

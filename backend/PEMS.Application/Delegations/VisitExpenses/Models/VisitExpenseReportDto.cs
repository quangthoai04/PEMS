using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.VisitExpenses.Models;

public class VisitExpenseReportDto
{
    public ulong ExpenseReportId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string ReportScope { get; set; } = null!;
    public ulong? LogisticsItemId { get; set; }
    public ulong? DepartmentId { get; set; }
    public string Status { get; set; } = null!;
    public string? ReportNote { get; set; }
    public bool NoExpense { get; set; }
    public string CurrencyCode { get; set; } = null!;

    // Display context for the Host summary view (filled by the summary query).
    public string? DepartmentName { get; set; }
    public string? LogisticsItemTitle { get; set; }
    public int RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Derived total
    public decimal TotalAmount { get; set; }

    public List<VisitExpenseItemDto> Items { get; set; } = new();
}

using MediatR;
using PEMS.Application.Delegations.VisitExpenses.Models;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.VisitExpenses.Commands.SaveExpenseReport;

public class SaveExpenseReportCommand : IRequest<VisitExpenseReportDto>
{
    public ulong ExpenseReportId { get; set; }
    public string? ReportNote { get; set; }
    public int RowVersion { get; set; }
    
    public List<SaveExpenseItemDto> Items { get; set; } = new();
}

public class SaveExpenseItemDto
{
    public ulong? ExpenseItemId { get; set; }
    public string ItemOrigin { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string? UnitName { get; set; }
    public decimal UnitPrice { get; set; }
    public string? ItemNote { get; set; }
    public int DisplayOrder { get; set; }
}

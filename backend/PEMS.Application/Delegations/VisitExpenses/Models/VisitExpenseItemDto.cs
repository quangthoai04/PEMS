using System;

namespace PEMS.Application.Delegations.VisitExpenses.Models;

public class VisitExpenseItemDto
{
    public ulong ExpenseItemId { get; set; }
    public string ItemOrigin { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string? UnitName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ItemNote { get; set; }
    public int DisplayOrder { get; set; }
    public int RowVersion { get; set; }
}

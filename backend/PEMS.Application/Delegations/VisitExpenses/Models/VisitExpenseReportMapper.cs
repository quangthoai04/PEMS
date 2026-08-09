using System.Linq;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitExpenses.Models;

/// <summary>
/// Entity → DTO for a single expense report, shared by the read and the initialize paths so the two
/// cannot drift into describing the same row differently.
/// </summary>
public static class VisitExpenseReportMapper
{
    public static VisitExpenseReportDto ToDto(VisitExpenseReport entity) => new()
    {
        ExpenseReportId = entity.ExpenseReportId,
        VisitInstanceId = entity.VisitInstanceId,
        ReportScope = entity.ReportScope,
        LogisticsItemId = entity.LogisticsItemId,
        DepartmentId = entity.DepartmentId,
        Status = entity.Status,
        ReportNote = entity.ReportNote,
        NoExpense = entity.NoExpense,
        CurrencyCode = entity.CurrencyCode,
        RowVersion = entity.RowVersion,
        CreatedAt = entity.CreatedAt,
        TotalAmount = entity.Items.Where(i => i.ItemOrigin != "CANCELLED").Sum(i => i.Quantity * i.UnitPrice),
        Items = entity.Items.OrderBy(i => i.DisplayOrder).Select(i => new VisitExpenseItemDto
        {
            ExpenseItemId = i.ExpenseItemId,
            ItemOrigin = i.ItemOrigin,
            ItemName = i.ItemName,
            Description = i.Description,
            Quantity = i.Quantity,
            UnitName = i.UnitName,
            UnitPrice = i.UnitPrice,
            TotalAmount = i.Quantity * i.UnitPrice,
            ItemNote = i.ItemNote,
            DisplayOrder = i.DisplayOrder,
            RowVersion = i.RowVersion,
        }).ToList(),
    };
}

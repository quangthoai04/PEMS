using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_expense_items")]
public class VisitExpenseItem
{
    [Key]
    [Column("expense_item_id")]
    public ulong ExpenseItemId { get; set; }

    [Column("expense_report_id")]
    public ulong ExpenseReportId { get; set; }

    [Column("item_origin")]
    public string ItemOrigin { get; set; } = "MANUAL";

    [Column("item_name")]
    public string ItemName { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; }

    [Column("unit_name")]
    public string? UnitName { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; private set; } // Database computed

    [Column("item_note")]
    public string? ItemNote { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("row_version")]
    public int RowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual VisitExpenseReport ExpenseReport { get; set; } = null!;
}

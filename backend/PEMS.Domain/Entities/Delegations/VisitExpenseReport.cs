using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_expense_reports")]
public class VisitExpenseReport
{
    [Key]
    [Column("expense_report_id")]
    public ulong ExpenseReportId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("report_scope")]
    public string ReportScope { get; set; } = null!;

    [Column("logistics_item_id")]
    public ulong? LogisticsItemId { get; set; }

    [Column("department_id")]
    public ulong? DepartmentId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "DRAFT";

    [Column("report_note")]
    public string? ReportNote { get; set; }

    [Column("currency_code")]
    public string CurrencyCode { get; set; } = "VND";

    [Column("saved_at")]
    public DateTime? SavedAt { get; set; }

    [Column("saved_by")]
    public ulong? SavedBy { get; set; }

    [Column("finalized_at")]
    public DateTime? FinalizedAt { get; set; }

    [Column("finalized_by")]
    public ulong? FinalizedBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancelled_by")]
    public ulong? CancelledBy { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

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

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;
    public virtual VisitLogisticsItem? LogisticsItem { get; set; }
    
    public virtual ICollection<VisitExpenseItem> Items { get; set; } = new List<VisitExpenseItem>();
    public virtual ICollection<VisitExpenseReportEvent> Events { get; set; } = new List<VisitExpenseReportEvent>();
}

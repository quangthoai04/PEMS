using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_expense_report_events")]
public class VisitExpenseReportEvent
{
    [Key]
    [Column("expense_event_id")]
    public ulong ExpenseEventId { get; set; }

    [Column("expense_report_id")]
    public ulong ExpenseReportId { get; set; }

    [Column("event_type")]
    public string EventType { get; set; } = null!;

    [Column("event_note")]
    public string? EventNote { get; set; }

    [Column("snapshot_json")]
    public string? SnapshotJson { get; set; }

    [Column("performed_at")]
    public DateTime PerformedAt { get; set; }

    [Column("performed_by")]
    public ulong? PerformedBy { get; set; }

    public virtual VisitExpenseReport ExpenseReport { get; set; } = null!;
}

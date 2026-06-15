using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("visit_status_logs")]
public class VisitStatusLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("old_status")]
    public string? OldStatus { get; set; }

    [Column("new_status")]
    public string NewStatus { get; set; } = null!;

    [Column("changed_by")]
    public string? ChangedBy { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
}

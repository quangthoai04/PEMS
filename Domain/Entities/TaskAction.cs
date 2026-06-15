using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("task_actions")]
public class TaskAction
{
    [Key]
    [Column("action_id")]
    public string ActionId { get; set; } = null!;

    [Column("task_id")]
    public string TaskId { get; set; } = null!;

    [Column("action_type")]
    public string ActionType { get; set; } = null!;

    [Column("approved_by")]
    public string? ApprovedBy { get; set; }

    [Column("signature_date")]
    public DateTime? SignatureDate { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    public virtual PemsTask Task { get; set; } = null!;
}

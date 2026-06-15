using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("action_items")]
public class ActionItem
{
    [Key]
    [Column("action_item_id")]
    public string ActionItemId { get; set; } = null!;

    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("assignee_user_id")]
    public string? AssigneeUserId { get; set; }

    [Column("due_date")]
    public DateOnly? DueDate { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Open";

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
}

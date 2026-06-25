using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Minutes;

[Table("minute_action_items")]
public class MinuteActionItem
{
    [Key]
    [Column("action_item_id")]
    public ulong ActionItemId { get; set; }

    [Column("minutes_id")]
    public ulong MinutesId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("note")]
    public string? Note { get; set; }

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    // minute_action_items.status ENUM('TODO','IN_PROGRESS','DONE','CANCELLED') DEFAULT 'TODO'
    [Column("status")]
    public string Status { get; set; } = "TODO";

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual Minute Minute { get; set; } = null!;
}

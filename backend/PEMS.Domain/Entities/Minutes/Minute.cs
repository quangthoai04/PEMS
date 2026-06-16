using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Minutes;

[Table("minutes")]
public class Minute
{
    [Key]
    [Column("minutes_id")]
    public string MinutesId { get; set; } = null!;

    [Column("visit_instance_id")]
    public string VisitInstanceId { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("content")]
    public string? Content { get; set; }

    [Column("participants_json")]
    public string? ParticipantsJson { get; set; }

    [Column("attachments_json")]
    public string? AttachmentsJson { get; set; }

    [Column("action_items_json")]
    public string? ActionItemsJson { get; set; }

    [Column("status")]
    public string Status { get; set; } = "DRAFT";

    [Column("finalized_by")]
    public string? FinalizedBy { get; set; }

    [Column("finalized_at")]
    public DateTime? FinalizedAt { get; set; }

    [Column("editing_by")]
    public string? EditingBy { get; set; }

    [Column("editing_started_at")]
    public DateTime? EditingStartedAt { get; set; }

    [Column("editing_until")]
    public DateTime? EditingUntil { get; set; }

    [Column("edit_lock_token")]
    public string? EditLockToken { get; set; }

    [Column("row_version")]
    public int RowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Minutes;

[Table("minutes")]
public class Minute
{
    [Key]
    [Column("minutes_id")]
    public ulong MinutesId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("content")]
    public string? Content { get; set; }

    [Column("participants_json")]
    public string? ParticipantsJson { get; set; }

    [Column("status")]
    public string Status { get; set; } = "DRAFT";

    [Column("finalized_by")]
    public ulong? FinalizedBy { get; set; }

    [Column("finalized_at")]
    public DateTime? FinalizedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual ICollection<MinuteActionItem> ActionItems { get; set; } = new List<MinuteActionItem>();
}

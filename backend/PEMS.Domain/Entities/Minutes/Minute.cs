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


    [Column("status")]
    public string Status { get; set; } = "DRAFT";

    [Column("edit_locked_by")]
    public ulong? EditLockedBy { get; set; }

    [Column("edit_locked_at")]
    public DateTime? EditLockedAt { get; set; }

    [Column("edit_lock_expires_at")]
    public DateTime? EditLockExpiresAt { get; set; }

    [Column("edit_lock_token")]
    public string? EditLockToken { get; set; }

    [Column("row_version")]
    public uint RowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual ICollection<MinuteActionItem> ActionItems { get; set; } = new List<MinuteActionItem>();
    public virtual ICollection<MinuteParticipant> Participants { get; set; } = new List<MinuteParticipant>();
}

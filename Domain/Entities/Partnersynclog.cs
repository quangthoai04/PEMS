using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("partner_sync_logs")]
public class PartnerSyncLog
{
    [Key]
    [Column("sync_id")]
    public string SyncId { get; set; } = null!;

    [Column("partner_id")]
    public string? PartnerId { get; set; }

    [Column("sync_direction")]
    public string SyncDirection { get; set; } = null!;

    [Column("sync_status")]
    public string SyncStatus { get; set; } = null!;

    [Column("message")]
    public string? Message { get; set; }

    [Column("synced_at")]
    public DateTime SyncedAt { get; set; }

    public virtual Partner? Partner { get; set; }
}

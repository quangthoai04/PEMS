using PEMS.Domain.Entities.AgendaTemplates;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Faqs;
using PEMS.Domain.Entities.Feedbacks;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Minutes;
using PEMS.Domain.Entities.News;
using PEMS.Domain.Entities.Notifications;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Reports;
using PEMS.Domain.Entities.Tasks;
using PEMS.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Partners;

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

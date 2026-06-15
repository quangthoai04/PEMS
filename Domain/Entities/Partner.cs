using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("partners")]
public class Partner
{
    [Key]
    [Column("partner_id")]
    public string PartnerId { get; set; } = null!;

    [Column("code")]
    public string Code { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("country")]
    public string? Country { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Draft";

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("website")]
    public string? Website { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    [Column("cover_url")]
    public string? CoverUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<PartnerContact> Contacts { get; set; } = new List<PartnerContact>();
    public virtual ICollection<PartnerHistory> Histories { get; set; } = new List<PartnerHistory>();
    public virtual ICollection<PartnerDocument> Documents { get; set; } = new List<PartnerDocument>();
    public virtual ICollection<PartnerSyncLog> SyncLogs { get; set; } = new List<PartnerSyncLog>();
}

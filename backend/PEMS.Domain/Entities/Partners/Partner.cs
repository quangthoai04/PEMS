using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Partners;

[Table("partners")]
public class Partner
{
    [Key]
    [Column("partner_id")]
    public ulong PartnerId { get; set; }

    [Column("partner_code")]
    public string PartnerCode { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("short_name")]
    public string? ShortName { get; set; }

    [Column("partner_type")]
    public string PartnerType { get; set; } = null!;

    [Column("country")]
    public string? Country { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("website_url")]
    public string? WebsiteUrl { get; set; }

    [Column("logo_file_id")]
    public ulong? LogoFileId { get; set; }

    [Column("cover_file_id")]
    public ulong? CoverFileId { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("public_slug")]
    public string? PublicSlug { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("cooperation_status")]
    public string CooperationStatus { get; set; } = "ACTIVE";

    [Column("profile_status")]
    public string ProfileStatus { get; set; } = "APPROVED";

    [Column("review_note")]
    public string? ReviewNote { get; set; }

    [Column("reviewed_by")]
    public ulong? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("visibility")]
    public string Visibility { get; set; } = "PUBLIC";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual ICollection<PartnerContact> Contacts { get; set; } = new List<PartnerContact>();
}

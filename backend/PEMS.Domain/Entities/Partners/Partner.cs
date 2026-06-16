using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Partners;

[Table("partners")]
public class Partner
{
    [Key]
    [Column("partner_id")]
    public string PartnerId { get; set; } = null!;

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

    [Column("description")]
    public string? Description { get; set; }

    [Column("cooperation_status")]
    public string CooperationStatus { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    public virtual ICollection<PartnerContact> Contacts { get; set; } = new List<PartnerContact>();
}

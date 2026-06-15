using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("partner_contacts")]
public class PartnerContact
{
    [Key]
    [Column("contact_id")]
    public string ContactId { get; set; } = null!;

    [Column("partner_id")]
    public string PartnerId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("email")]
    public string? Email { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("role_title")]
    public string? RoleTitle { get; set; }

    [Column("department")]
    public string? Department { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    public virtual Partner Partner { get; set; } = null!;
}

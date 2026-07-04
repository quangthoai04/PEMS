using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Partners;

/// <summary>
/// Alternative name of a partner, used to match organization names coming from
/// visit guests / business-card OCR. alias_name_key is the normalized form
/// (lower-case, no accents/punctuation, collapsed spaces).
/// </summary>
[Table("partner_aliases")]
public class PartnerAlias
{
    [Key]
    [Column("partner_alias_id")]
    public ulong PartnerAliasId { get; set; }

    [Column("partner_id")]
    public ulong PartnerId { get; set; }

    [Column("alias_name")]
    public string AliasName { get; set; } = null!;

    [Column("alias_name_key")]
    public string AliasNameKey { get; set; } = null!;

    /// <summary>MANUAL | OCR | AUTO_MATCH | IMPORT</summary>
    [Column("source")]
    public string Source { get; set; } = "MANUAL";

    /// <summary>ACTIVE | INACTIVE</summary>
    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual Partner Partner { get; set; } = null!;
}

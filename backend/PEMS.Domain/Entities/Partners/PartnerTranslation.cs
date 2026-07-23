using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Partners;

/// <summary>
/// Per-language Partner content (name/short_name/country/city/description/address). One row per
/// (partner_id, language_code); 'vi' mirrors the canonical <see cref="Partner"/> row, 'en' is
/// produced once at create/update time (never at read time) by Google Translation or entered/
/// edited manually by the admin. country/city are proper nouns copied as-is (not machine
/// translated) — see CreatePartnerCommandHandler/UpdatePartnerCommandHandler.
/// </summary>
[Table("partner_translations")]
public class PartnerTranslation
{
    [Key]
    [Column("partner_translation_id")]
    public ulong PartnerTranslationId { get; set; }

    [Column("partner_id")]
    public ulong PartnerId { get; set; }

    [Column("language_code")]
    public string LanguageCode { get; set; } = "vi";

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("short_name")]
    public string? ShortName { get; set; }

    [Column("country")]
    public string? Country { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    /// <summary>AUTO | MANUAL | LEGACY.</summary>
    [Column("translation_source")]
    public string TranslationSource { get; set; } = "AUTO";

    /// <summary>PENDING | READY | FAILED | OUTDATED.</summary>
    [Column("translation_status")]
    public string TranslationStatus { get; set; } = "PENDING";

    [Column("source_hash")]
    public string? SourceHash { get; set; }

    [Column("translated_at")]
    public DateTime? TranslatedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }
}

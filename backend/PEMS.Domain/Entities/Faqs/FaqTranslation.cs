using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Faqs;

/// <summary>
/// Per-language FAQ content (question/answer). One row per (faq_id, language_code); 'vi' mirrors
/// the canonical <see cref="Faq"/> row, 'en' is produced once at create/update time (never at
/// read time) by Google Translation or entered/edited manually by the admin.
/// </summary>
[Table("faq_translations")]
public class FaqTranslation
{
    [Key]
    [Column("faq_translation_id")]
    public ulong FaqTranslationId { get; set; }

    [Column("faq_id")]
    public ulong FaqId { get; set; }

    [Column("language_code")]
    public string LanguageCode { get; set; } = "vi";

    [Column("question")]
    public string Question { get; set; } = null!;

    [Column("answer")]
    public string Answer { get; set; } = null!;

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

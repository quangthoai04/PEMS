using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Domain.Entities.Galleries;

/// <summary>
/// Bilingual content of a gallery item: the Vietnamese + English descriptions and the two
/// Staff-Leader-uploaded audio recordings. Exactly one row per <see cref="GalleryItem"/> (PK =
/// <c>gallery_item_id</c>), and both languages are mandatory — this replaces the old EverAI TTS
/// narration entirely.
/// </summary>
[Table("gallery_item_contents")]
public class GalleryItemContent
{
    [Key]
    [Column("gallery_item_id")]
    public ulong GalleryItemId { get; set; }

    [Column("description_vi")]
    public string DescriptionVi { get; set; } = null!;

    [Column("audio_vi_file_id")]
    public ulong AudioViFileId { get; set; }

    [Column("description_en")]
    public string DescriptionEn { get; set; } = null!;

    [Column("audio_en_file_id")]
    public ulong AudioEnFileId { get; set; }

    /// <summary>AUTO | MANUAL.</summary>
    [Column("translation_source")]
    public string? TranslationSource { get; set; }

    /// <summary>PENDING | READY | FAILED | OUTDATED.</summary>
    [Column("translation_status")]
    public string TranslationStatus { get; set; } = "PENDING";

    [Column("translation_source_hash")]
    public string? TranslationSourceHash { get; set; }

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

    public virtual GalleryItem GalleryItem { get; set; } = null!;
    public virtual UploadedFile AudioViFile { get; set; } = null!;
    public virtual UploadedFile AudioEnFile { get; set; } = null!;
}

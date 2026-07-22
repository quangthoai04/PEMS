using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Domain.Entities.Galleries;

[Table("gallery_item_media")]
public class GalleryItemMedia
{
    [Key]
    [Column("media_id")]
    public ulong MediaId { get; set; }

    [Column("gallery_item_id")]
    public ulong GalleryItemId { get; set; }

    [Column("file_id")]
    public ulong FileId { get; set; }

    [Column("media_type")]
    public string MediaType { get; set; } = null!;

    [Column("thumbnail_file_id")]
    public ulong? ThumbnailFileId { get; set; }

    [Column("caption")]
    public string? Caption { get; set; }

    /// <summary>Caption tiếng Anh đã dịch và lưu trong DB.</summary>
    [Column("caption_en")]
    public string? CaptionEn { get; set; }

    [Column("alt_text")]
    public string? AltText { get; set; }

    /// <summary>Alt text tiếng Anh đã dịch và lưu trong DB.</summary>
    [Column("alt_text_en")]
    public string? AltTextEn { get; set; }

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

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

    [Column("taken_at")]
    public DateTime? TakenAt { get; set; }

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

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public ulong? DeletedBy { get; set; }

    public virtual GalleryItem GalleryItem { get; set; } = null!;
    public virtual UploadedFile File { get; set; } = null!;
    public virtual UploadedFile? ThumbnailFile { get; set; }
}

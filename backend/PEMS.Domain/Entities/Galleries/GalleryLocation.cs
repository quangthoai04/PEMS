using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Galleries;

[Table("gallery_locations")]
public class GalleryLocation
{
    [Key]
    [Column("location_id")]
    public ulong LocationId { get; set; }

    [Column("area_id")]
    public ulong AreaId { get; set; }

    [Column("location_name")]
    public string LocationName { get; set; } = null!;

    /// <summary>Tên tiếng Anh đã dịch và lưu trong DB.</summary>
    [Column("location_name_en")]
    public string? LocationNameEn { get; set; }

    [Column("location_key")]
    public string LocationKey { get; set; } = null!;

    /// <summary>Ảnh đại diện vị trí cụ thể (master data, không phải gallery item). FK → files.file_id.</summary>
    [Column("cover_file_id")]
    public ulong? CoverFileId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

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

    public virtual GalleryArea Area { get; set; } = null!;
    public virtual ICollection<GalleryItem> Items { get; set; } = new List<GalleryItem>();
}

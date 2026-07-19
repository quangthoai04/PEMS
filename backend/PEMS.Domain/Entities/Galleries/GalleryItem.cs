using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Galleries;

[Table("gallery_items")]
public class GalleryItem
{
    [Key]
    [Column("gallery_item_id")]
    public ulong GalleryItemId { get; set; }

    [Column("location_id")]
    public ulong LocationId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    /// <summary>MEDIA (ảnh/video giới thiệu vị trí) hoặc VISIT_DELEGATION (ảnh/video đoàn khách).</summary>
    [Column("item_type")]
    public string ItemType { get; set; } = "MEDIA";

    [Column("media_kind")]
    public string MediaKind { get; set; } = "IMAGE";

    [Column("status")]
    public string Status { get; set; } = "PUBLISHED";

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

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

    public virtual GalleryLocation Location { get; set; } = null!;
    public virtual ICollection<GalleryItemMedia> Media { get; set; } = new List<GalleryItemMedia>();

    /// <summary>The item's mandatory bilingual content (VI/EN descriptions + audio). 1:1.</summary>
    public virtual GalleryItemContent Content { get; set; } = null!;
}

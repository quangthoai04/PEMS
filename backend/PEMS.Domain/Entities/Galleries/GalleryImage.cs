using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Galleries;

[Table("gallery_images")]
public class GalleryImage
{
    [Key]
    [Column("image_id")]
    public ulong ImageId { get; set; }

    [Column("gallery_id")]
    public ulong GalleryId { get; set; }

    [Column("file_id")]
    public ulong FileId { get; set; }

    [Column("media_type")]
    public string MediaType { get; set; } = "IMAGE";

    [Column("thumbnail_file_id")]
    public ulong? ThumbnailFileId { get; set; }

    [Column("caption")]
    public string? Caption { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }

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

    public virtual Gallery Gallery { get; set; } = null!;
}

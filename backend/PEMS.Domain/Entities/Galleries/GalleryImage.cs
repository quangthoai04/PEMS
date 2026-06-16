using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Galleries;

[Table("gallery_images")]
public class GalleryImage
{
    [Key]
    [Column("image_id")]
    public string ImageId { get; set; } = null!;

    [Column("gallery_id")]
    public string GalleryId { get; set; } = null!;

    [Column("file_id")]
    public string FileId { get; set; } = null!;

    [Column("caption")]
    public string? Caption { get; set; }

    [Column("location_name")]
    public string? LocationName { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("taken_at")]
    public DateTime? TakenAt { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public string? DeletedBy { get; set; }

    public virtual Gallery Gallery { get; set; } = null!;
}

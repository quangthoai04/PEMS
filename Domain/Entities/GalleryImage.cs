using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("gallery_images")]
public class GalleryImage
{
    [Key]
    [Column("image_id")]
    public string ImageId { get; set; } = null!;

    [Column("gallery_id")]
    public string GalleryId { get; set; } = null!;

    [Column("url")]
    public string Url { get; set; } = null!;

    [Column("caption")]
    public string? Caption { get; set; }

    [Column("upload_date")]
    public DateTime UploadDate { get; set; }

    public virtual Gallery Gallery { get; set; } = null!;
}

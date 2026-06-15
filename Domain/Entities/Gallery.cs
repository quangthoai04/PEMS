using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("galleries")]
public class Gallery
{
    [Key]
    [Column("gallery_id")]
    public string GalleryId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("uploaded_by")]
    public string? UploadedBy { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<GalleryImage> Images { get; set; } = new List<GalleryImage>();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("gallery_location_images")]
public class GalleryLocationImage
{
    [Key]
    [Column("gli_id")]
    public string GliId { get; set; } = null!;

    [Column("location_id")]
    public string LocationId { get; set; } = null!;

    [Column("url")]
    public string Url { get; set; } = null!;

    [Column("caption")]
    public string? Caption { get; set; }

    public virtual GalleryLocation Location { get; set; } = null!;
}

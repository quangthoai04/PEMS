using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("gallery_locations")]
public class GalleryLocation
{
    [Key]
    [Column("location_id")]
    public string LocationId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    public virtual ICollection<GalleryLocationImage> Images { get; set; } = new List<GalleryLocationImage>();
}

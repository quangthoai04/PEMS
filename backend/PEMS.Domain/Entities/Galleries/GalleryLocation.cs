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

    [Column("location_key")]
    public string LocationKey { get; set; } = null!;

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

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

    public virtual GalleryArea Area { get; set; } = null!;
    public virtual ICollection<GalleryItem> Items { get; set; } = new List<GalleryItem>();
}

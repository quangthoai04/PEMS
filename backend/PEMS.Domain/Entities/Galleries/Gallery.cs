using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Galleries;

[Table("galleries")]
public class Gallery
{
    [Key]
    [Column("gallery_id")]
    public ulong GalleryId { get; set; }

    [Column("campus_id")]
    public ulong? CampusId { get; set; }

    [Column("area_name")]
    public string AreaName { get; set; } = null!;

    [Column("specific_location_name")]
    public string SpecificLocationName { get; set; } = null!;

    [Column("visit_instance_id")]
    public ulong? VisitInstanceId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("location_description")]
    public string? LocationDescription { get; set; }

    [Column("hero_file_id")]
    public ulong? HeroFileId { get; set; }

    [Column("virtual_tour_url")]
    public string? VirtualTourUrl { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("visibility")]
    public string Visibility { get; set; } = "INTERNAL";

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

    public virtual ICollection<GalleryImage> Images { get; set; } = new List<GalleryImage>();
}

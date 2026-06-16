using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Galleries;

[Table("galleries")]
public class Gallery
{
    [Key]
    [Column("gallery_id")]
    public string GalleryId { get; set; } = null!;

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("visit_instance_id")]
    public string? VisitInstanceId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("visibility")]
    public string Visibility { get; set; } = "INTERNAL";

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

    public virtual ICollection<GalleryImage> Images { get; set; } = new List<GalleryImage>();
}

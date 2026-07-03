using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Campuses;

namespace PEMS.Domain.Entities.Galleries;

[Table("gallery_areas")]
public class GalleryArea
{
    [Key]
    [Column("area_id")]
    public ulong AreaId { get; set; }

    [Column("campus_id")]
    public ulong CampusId { get; set; }

    [Column("area_name")]
    public string AreaName { get; set; } = null!;

    [Column("area_key")]
    public string AreaKey { get; set; } = null!;

    /// <summary>Ảnh đại diện khu vực (master data, không phải gallery item). FK → files.file_id.</summary>
    [Column("cover_file_id")]
    public ulong? CoverFileId { get; set; }

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

    public virtual Campus Campus { get; set; } = null!;
    public virtual ICollection<GalleryLocation> Locations { get; set; } = new List<GalleryLocation>();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Galleries;

[Table("photo_face_tags")]
public class PhotoFaceTag
{
    [Key]
    [Column("face_tag_id")]
    public ulong FaceTagId { get; set; }

    [Column("image_id")]
    public ulong ImageId { get; set; }

    [Column("visit_request_id")]
    public ulong? VisitRequestId { get; set; }

    [Column("guest_member_id")]
    public ulong? GuestMemberId { get; set; }

    [Column("partner_contact_id")]
    public ulong? PartnerContactId { get; set; }

    [Column("display_name")]
    public string DisplayName { get; set; } = null!;

    [Column("bounding_box_x")]
    public decimal? BoundingBoxX { get; set; }

    [Column("bounding_box_y")]
    public decimal? BoundingBoxY { get; set; }

    [Column("bounding_box_width")]
    public decimal? BoundingBoxWidth { get; set; }

    [Column("bounding_box_height")]
    public decimal? BoundingBoxHeight { get; set; }

    [Column("tag_status")]
    public string TagStatus { get; set; } = "MANUALLY_TAGGED";

    [Column("confirmed_by")]
    public ulong? ConfirmedBy { get; set; }

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("removed_at")]
    public DateTime? RemovedAt { get; set; }

    [Column("removed_by")]
    public ulong? RemovedBy { get; set; }

    public virtual GalleryImage Image { get; set; } = null!;
}

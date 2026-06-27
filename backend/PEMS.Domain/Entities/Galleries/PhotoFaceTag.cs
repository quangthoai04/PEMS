using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Users;

namespace PEMS.Domain.Entities.Galleries;

[Table("photo_face_tags")]
public class PhotoFaceTag
{
    [Key]
    [Column("face_tag_id")]
    public ulong FaceTagId { get; set; }

    [Column("file_id")]
    public ulong FileId { get; set; }

    [Column("tagged_user_id")]
    public ulong? TaggedUserId { get; set; }

    [Column("visit_request_id")]
    public ulong? VisitRequestId { get; set; }

    [Column("guest_member_id")]
    public ulong? GuestMemberId { get; set; }

    [Column("partner_contact_id")]
    public ulong? PartnerContactId { get; set; }

    [Column("display_name")]
    public string DisplayName { get; set; } = null!;

    [Column("person_name_key")]
    public string? PersonNameKey { get; set; }

    [Column("bounding_box_x")]
    public decimal? BoundingBoxX { get; set; }

    [Column("bounding_box_y")]
    public decimal? BoundingBoxY { get; set; }

    [Column("bounding_box_width")]
    public decimal? BoundingBoxWidth { get; set; }

    [Column("bounding_box_height")]
    public decimal? BoundingBoxHeight { get; set; }

    [Column("tag_status")]
    public string TagStatus { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("removed_at")]
    public DateTime? RemovedAt { get; set; }

    [Column("removed_by")]
    public ulong? RemovedBy { get; set; }

    public virtual UploadedFile File { get; set; } = null!;
    public virtual User? TaggedUser { get; set; }
    public virtual VisitRequest? VisitRequest { get; set; }
    public virtual VisitGuestMember? GuestMember { get; set; }
    public virtual PartnerContact? PartnerContact { get; set; }
}

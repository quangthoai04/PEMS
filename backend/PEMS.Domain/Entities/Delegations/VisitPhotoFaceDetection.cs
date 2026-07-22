using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Users;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// One Google Vision face box detected inside a <see cref="VisitPhotoFaceScan"/>, awaiting manual
/// assignment to a guest of the exact campus visit instance (visit_photo_face_detections).
/// Bounding box is normalized 0..1 (fraction of image width/height). Confirmed rows link to the
/// canonical <see cref="PhotoFaceTag"/> row created at confirmation time.
/// </summary>
[Table("visit_photo_face_detections")]
public class VisitPhotoFaceDetection
{
    public const string ReviewStatusDetected = "DETECTED";
    public const string ReviewStatusConfirmed = "CONFIRMED";
    public const string ReviewStatusIgnored = "IGNORED";

    [Key]
    [Column("face_detection_id")]
    public ulong FaceDetectionId { get; set; }

    [Column("face_scan_id")]
    public ulong FaceScanId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("file_id")]
    public ulong FileId { get; set; }

    /// <summary>One-based stable index inside this scan result.</summary>
    [Column("face_index")]
    public uint FaceIndex { get; set; }

    /// <summary>Normalized left coordinate from 0 to 1.</summary>
    [Column("bounding_box_x")]
    public decimal BoundingBoxX { get; set; }

    /// <summary>Normalized top coordinate from 0 to 1.</summary>
    [Column("bounding_box_y")]
    public decimal BoundingBoxY { get; set; }

    /// <summary>Normalized width from 0 to 1.</summary>
    [Column("bounding_box_width")]
    public decimal BoundingBoxWidth { get; set; }

    /// <summary>Normalized height from 0 to 1.</summary>
    [Column("bounding_box_height")]
    public decimal BoundingBoxHeight { get; set; }

    /// <summary>Google Vision detection confidence from 0 to 1.</summary>
    [Column("detection_confidence")]
    public decimal DetectionConfidence { get; set; }

    /// <summary>Manual selection; must belong to this exact visit instance.</summary>
    [Column("guest_member_id")]
    public ulong? GuestMemberId { get; set; }

    /// <summary>Final canonical photo_face_tags row created after confirmation.</summary>
    [Column("face_tag_id")]
    public ulong? FaceTagId { get; set; }

    /// <summary>DETECTED | CONFIRMED | IGNORED</summary>
    [Column("review_status")]
    public string ReviewStatus { get; set; } = ReviewStatusDetected;

    [Column("review_note")]
    public string? ReviewNote { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by")]
    public ulong? ReviewedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public virtual VisitPhotoFaceScan FaceScan { get; set; } = null!;
    public virtual PhotoFaceTag? FaceTag { get; set; }
    public virtual User? ReviewedByUser { get; set; }
}

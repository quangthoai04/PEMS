using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// One Google Cloud Vision FACE_DETECTION execution for one private visit photo
/// (visit_photo_face_scans). Stores scan state/counts and normalized bounding boxes only —
/// no face embedding, no automatic identity recognition, no emotion data. The composite FK to
/// <see cref="VisitPhoto"/> (visit_photo_id, visit_request_id, visit_instance_id, file_id) proves
/// the scan belongs to that exact business photo.
/// </summary>
[Table("visit_photo_face_scans")]
public class VisitPhotoFaceScan
{
    public const string StatusPending = "PENDING";
    public const string StatusProcessing = "PROCESSING";
    public const string StatusSucceeded = "SUCCEEDED";
    public const string StatusFailed = "FAILED";
    public const string StatusConfirmed = "CONFIRMED";

    [Key]
    [Column("face_scan_id")]
    public ulong FaceScanId { get; set; }

    [Column("visit_photo_id")]
    public ulong VisitPhotoId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("file_id")]
    public ulong FileId { get; set; }

    [Column("api_config_id")]
    public ulong ApiConfigId { get; set; }

    /// <summary>PENDING | PROCESSING | SUCCEEDED | FAILED | CONFIRMED</summary>
    [Column("status")]
    public string Status { get; set; } = StatusPending;

    [Column("provider_name")]
    public string ProviderName { get; set; } = "GOOGLE_CLOUD_VISION";

    [Column("provider_request_id")]
    public string? ProviderRequestId { get; set; }

    [Column("feature_type")]
    public string FeatureType { get; set; } = "FACE_DETECTION";

    [Column("image_width")]
    public uint? ImageWidth { get; set; }

    [Column("image_height")]
    public uint? ImageHeight { get; set; }

    [Column("detected_face_count")]
    public uint DetectedFaceCount { get; set; }

    [Column("reviewed_face_count")]
    public uint ReviewedFaceCount { get; set; }

    [Column("ignored_face_count")]
    public uint IgnoredFaceCount { get; set; }

    [Column("error_code")]
    public string? ErrorCode { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("requested_by")]
    public ulong? RequestedBy { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    [Column("confirmed_by")]
    public ulong? ConfirmedBy { get; set; }

    [Column("row_version")]
    public uint RowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public virtual VisitPhoto VisitPhoto { get; set; } = null!;
    public virtual ApiConfiguration ApiConfiguration { get; set; } = null!;
    public virtual User? RequestedByUser { get; set; }
    public virtual User? ConfirmedByUser { get; set; }
    public virtual ICollection<VisitPhotoFaceDetection> Detections { get; set; } = new List<VisitPhotoFaceDetection>();
}

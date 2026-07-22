using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// A private visit/delegation photo uploaded by an ACCEPTED Student participant of the exact campus
/// instance. Binary lives on Google Drive (via <c>files</c> with purpose VISIT_REQUEST_PHOTO);
/// this row only models business ownership. Soft-deleted via status REMOVED (removed_* required).
/// Independent from Gallery.
/// </summary>
[Table("visit_photos")]
public class VisitPhoto
{
    [Key]
    [Column("visit_photo_id")]
    public ulong VisitPhotoId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("visit_photo_folder_id")]
    public ulong VisitPhotoFolderId { get; set; }

    [Column("file_id")]
    public ulong FileId { get; set; }

    [Column("caption")]
    public string? Caption { get; set; }

    [Column("taken_at")]
    public DateTime? TakenAt { get; set; }

    // ENUM('ACTIVE','REMOVED')
    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("uploaded_by")]
    public ulong UploadedBy { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    [Column("removed_at")]
    public DateTime? RemovedAt { get; set; }

    [Column("removed_by")]
    public ulong? RemovedBy { get; set; }

    [Column("removal_reason")]
    public string? RemovalReason { get; set; }

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;
    public virtual VisitPhotoFolder Folder { get; set; } = null!;
    public virtual UploadedFile File { get; set; } = null!;
    public virtual ICollection<VisitPhotoFaceScan> FaceScans { get; set; } = new List<VisitPhotoFaceScan>();
}

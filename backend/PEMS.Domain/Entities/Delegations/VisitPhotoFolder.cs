using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Users;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// One private Google Drive child folder per visit request/delegation
/// (<c>VisitRequestPhotoFolderId</c> root → <c>{campus_code}</c> → <c>{RequestCode}</c> — Campus is
/// the outermost Drive level). For a delegation that visits several campuses at once
/// (VisitScope=MULTI_CAMPUS, rare), this single folder is still shared by all of them — it simply
/// lives under whichever campus's upload created it first; there is deliberately no per-campus
/// column here to keep this table at exactly one row per request. Independent from Gallery.
/// </summary>
[Table("visit_photo_folders")]
public class VisitPhotoFolder
{
    [Key]
    [Column("visit_photo_folder_id")]
    public ulong VisitPhotoFolderId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("storage_provider")]
    public string StorageProvider { get; set; } = "GOOGLE_DRIVE";

    [Column("external_folder_id")]
    public string ExternalFolderId { get; set; } = null!;

    [Column("folder_name")]
    public string FolderName { get; set; } = null!;

    [Column("web_view_url")]
    public string? WebViewUrl { get; set; }

    // ENUM('ACTIVE','ARCHIVED')
    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
    public virtual ICollection<VisitPhoto> Photos { get; set; } = new List<VisitPhoto>();
}

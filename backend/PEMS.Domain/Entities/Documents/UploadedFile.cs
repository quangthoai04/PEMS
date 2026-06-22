using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Documents;

[Table("files")]
public class UploadedFile
{
    [Key]
    [Column("file_id")]
    public ulong FileId { get; set; }

    [Column("storage_provider")]
    public string StorageProvider { get; set; } = "LOCAL";

    [Column("bucket_name")]
    public string? BucketName { get; set; }

    [Column("object_key")]
    public string ObjectKey { get; set; } = null!;

    [Column("original_filename")]
    public string OriginalFilename { get; set; } = null!;

    [Column("mime_type")]
    public string? MimeType { get; set; }

    [Column("file_size")]
    public long? FileSize { get; set; }

    [Column("checksum_sha256")]
    public string? ChecksumSha256 { get; set; }

    [Column("external_file_id")]
    public string? ExternalFileId { get; set; }

    [Column("web_view_url")]
    public string? WebViewUrl { get; set; }

    [Column("download_url")]
    public string? DownloadUrl { get; set; }

    [Column("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }

    [Column("file_purpose")]
    public string? FilePurpose { get; set; }
    [Column("uploaded_by")]
    public ulong? UploadedBy { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}

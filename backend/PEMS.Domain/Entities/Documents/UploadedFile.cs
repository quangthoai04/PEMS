using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Documents;

[Table("files")]
public class UploadedFile
{
    [Key]
    [Column("file_id")]
    public string FileId { get; set; } = null!;

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

    [Column("visibility")]
    public string Visibility { get; set; } = "PRIVATE";

    [Column("uploaded_by")]
    public string? UploadedBy { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}

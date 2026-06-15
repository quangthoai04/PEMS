using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("partner_documents")]
public class PartnerDocument
{
    [Key]
    [Column("doc_id")]
    public string DocId { get; set; } = null!;

    [Column("partner_id")]
    public string PartnerId { get; set; } = null!;

    [Column("file_name")]
    public string FileName { get; set; } = null!;

    [Column("file_size")]
    public string? FileSize { get; set; }

    [Column("file_type")]
    public string? FileType { get; set; }

    [Column("file_url")]
    public string FileUrl { get; set; } = null!;

    [Column("upload_date")]
    public DateOnly UploadDate { get; set; }

    [Column("uploaded_by")]
    public string? UploadedBy { get; set; }

    public virtual Partner Partner { get; set; } = null!;
}

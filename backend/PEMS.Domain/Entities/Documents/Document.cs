using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Documents;

[Table("documents")]
public class Document
{
    [Key]
    [Column("document_id")]
    public string DocumentId { get; set; } = null!;

    [Column("file_id")]
    public string FileId { get; set; } = null!;

    [Column("owner_type")]
    public string OwnerType { get; set; } = "GENERAL";

    [Column("owner_id")]
    public string? OwnerId { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("document_category")]
    public string? DocumentCategory { get; set; }

    [Column("status")]
    public string Status { get; set; } = "DRAFT";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    public virtual UploadedFile File { get; set; } = null!;
}

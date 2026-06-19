using PEMS.Domain.Entities.Documents;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.News;

[Table("news_section_files")]
public class NewsSectionFile
{
    [Key]
    [Column("section_file_id")]
    public ulong SectionFileId { get; set; }

    [Column("section_id")]
    public ulong SectionId { get; set; }

    [Column("file_id")]
    public ulong FileId { get; set; }

    [Column("usage_type")]
    public string UsageType { get; set; } = "ATTACHMENT";

    [Column("display_order")]
    public int DisplayOrder { get; set; } = 0;

    public virtual NewsContentSection Section { get; set; } = null!;
    public virtual UploadedFile File { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.News;

[Table("news_content_sections")]
public class NewsContentSection
{
    [Key]
    [Column("section_id")]
    public ulong SectionId { get; set; }

    [Column("news_id")]
    public ulong NewsId { get; set; }

    [Column("section_order")]
    public int SectionOrder { get; set; }

    [Column("section_title")]
    public string? SectionTitle { get; set; }

    [Column("section_body_html")]
    public string? SectionBodyHtml { get; set; }

    [Column("section_body_text")]
    public string? SectionBodyText { get; set; }

    public virtual News News { get; set; } = null!;
    public virtual ICollection<NewsSectionFile> SectionFiles { get; set; } = new List<NewsSectionFile>();
}

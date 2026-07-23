using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.News;

[Table("news_content_sections")]
public class NewsContentSection
{
    [Key]
    [Column("section_id")]
    public ulong SectionId { get; set; }

    [Column("news_translation_id")]
    public ulong NewsTranslationId { get; set; }

    [Column("section_order")]
    public int SectionOrder { get; set; }

    /// <summary>SQL: section_title VARCHAR(255) NOT NULL.</summary>
    [Column("section_title")]
    public string SectionTitle { get; set; } = null!;

    /// <summary>SQL: section_body_html LONGTEXT NOT NULL (sanitized rich text).</summary>
    [Column("section_body_html")]
    public string SectionBodyHtml { get; set; } = null!;

    [Column("section_body_text")]
    public string? SectionBodyText { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public virtual NewsTranslation NewsTranslation { get; set; } = null!;
    public virtual ICollection<NewsSectionFile> SectionFiles { get; set; } = new List<NewsSectionFile>();
}

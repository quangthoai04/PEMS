using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.News;

[Table("news_translations")]
public class NewsTranslation
{
    [Key]
    [Column("news_translation_id")]
    public string NewsTranslationId { get; set; } = null!;

    [Column("news_id")]
    public string NewsId { get; set; } = null!;

    [Column("language_code")]
    public string LanguageCode { get; set; } = "vi";

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("slug")]
    public string Slug { get; set; } = null!;

    [Column("summary")]
    public string? Summary { get; set; }

    [Column("body")]
    public string? Body { get; set; }

    [Column("seo_title")]
    public string? SeoTitle { get; set; }

    [Column("seo_description")]
    public string? SeoDescription { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public virtual News News { get; set; } = null!;
}

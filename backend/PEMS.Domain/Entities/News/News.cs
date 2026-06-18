using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.News;

[Table("news")]
public class News
{
    [Key]
    [Column("news_id")]
    public string NewsId { get; set; } = null!;

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("author_user_id")]
    public string AuthorUserId { get; set; } = null!;

    [Column("cover_file_id")]
    public string? CoverFileId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "DRAFT";

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    [Column("decided_by")]
    public string? DecidedBy { get; set; }

    [Column("decided_at")]
    public DateTime? DecidedAt { get; set; }

    [Column("decision_note")]
    public string? DecisionNote { get; set; }

    [Column("is_featured")]
    public bool IsFeatured { get; set; }

    [Column("row_version")]
    public int RowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public string? DeletedBy { get; set; }

    public virtual ICollection<NewsTranslation> Translations { get; set; } = new List<NewsTranslation>();
    public virtual ICollection<NewsContentSection> Sections { get; set; } = new List<NewsContentSection>();
}

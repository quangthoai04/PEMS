using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.News;

[Table("news")]
public class News
{
    [Key]
    [Column("news_id")]
    public ulong NewsId { get; set; }

    [Column("campus_id")]
    public ulong? CampusId { get; set; }

    [Column("author_user_id")]
    public ulong AuthorUserId { get; set; }

    [Column("cover_file_id")]
    public ulong? CoverFileId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "DRAFT";

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    [Column("decided_by")]
    public ulong? DecidedBy { get; set; }

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
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public ulong? DeletedBy { get; set; }

    public virtual ICollection<NewsTranslation> Translations { get; set; } = new List<NewsTranslation>();
    public virtual ICollection<NewsContentSection> Sections { get; set; } = new List<NewsContentSection>();
}

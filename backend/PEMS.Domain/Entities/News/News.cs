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

    [Column("visit_instance_id")]
    public ulong? VisitInstanceId { get; set; }

    [Column("author_user_id")]
    public ulong AuthorUserId { get; set; }

    [Column("cover_file_id")]
    public ulong? CoverFileId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "PENDING_REVIEW";

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [Column("reviewed_by")]
    public ulong? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("review_note")]
    public string? ReviewNote { get; set; }

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    [Column("is_featured")]
    public bool IsFeatured { get; set; }

    [Column("is_pinned")]
    public bool IsPinned { get; set; }

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

    public virtual ICollection<NewsTranslation> Translations { get; set; } = new List<NewsTranslation>();
}

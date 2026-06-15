using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("news")]
public class News
{
    [Key]
    [Column("news_id")]
    public string NewsId { get; set; } = null!;

    [Column("news_type")]
    public string NewsType { get; set; } = "News";

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("summary")]
    public string? Summary { get; set; }

    [Column("body")]
    public string? Body { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Cho Duyet";

    [Column("published_date")]
    public DateOnly? PublishedDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }
}

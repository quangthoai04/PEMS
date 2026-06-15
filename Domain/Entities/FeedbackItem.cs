using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("feedback_items")]
public class FeedbackItem
{
    [Key]
    [Column("item_id")]
    public string ItemId { get; set; } = null!;

    [Column("feedback_id")]
    public string FeedbackId { get; set; } = null!;

    [Column("reviewer_name")]
    public string? ReviewerName { get; set; }

    [Column("reviewer_user_id")]
    public string? ReviewerUserId { get; set; }

    [Column("rating")]
    public sbyte? Rating { get; set; }

    [Column("space_rating")]
    public sbyte? SpaceRating { get; set; }

    [Column("support_rating")]
    public sbyte? SupportRating { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("item_date")]
    public DateOnly? ItemDate { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;
}

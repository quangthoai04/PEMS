using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Feedbacks;

[Table("feedback_rating_items")]
public class FeedbackRatingItem
{
    [Key]
    [Column("feedback_rating_item_id")]
    public ulong FeedbackRatingItemId { get; set; }

    [Column("feedback_id")]
    public ulong FeedbackId { get; set; }

    [Column("criterion_code")]
    public string CriterionCode { get; set; } = null!;

    [Column("criterion_label")]
    public string CriterionLabel { get; set; } = null!;

    [Column("rating")]
    public byte Rating { get; set; }

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;
}

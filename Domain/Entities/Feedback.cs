using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("feedbacks")]
public class Feedback
{
    [Key]
    [Column("feedback_id")]
    public string FeedbackId { get; set; } = null!;

    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("guest_name")]
    public string? GuestName { get; set; }

    [Column("average_rating")]
    public decimal? AverageRating { get; set; }

    [Column("feedback_date")]
    public DateOnly? FeedbackDate { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
    public virtual ICollection<FeedbackItem> Items { get; set; } = new List<FeedbackItem>();
}

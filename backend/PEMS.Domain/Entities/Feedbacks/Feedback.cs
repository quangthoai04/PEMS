using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Feedbacks;

[Table("feedbacks")]
public class Feedback
{
    [Key]
    [Column("feedback_id")]
    public string FeedbackId { get; set; } = null!;

    [Column("visit_request_id")]
    public string VisitRequestId { get; set; } = null!;

    [Column("visit_instance_id")]
    public string? VisitInstanceId { get; set; }

    [Column("submitted_by_user_id")]
    public string? SubmittedByUserId { get; set; }

    [Column("guest_member_id")]
    public string? GuestMemberId { get; set; }

    [Column("rating")]
    public byte? Rating { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("answers_json")]
    public string? AnswersJson { get; set; }

    [Column("rating_details_json")]
    public string? RatingDetailsJson { get; set; }

    [Column("status")]
    public string Status { get; set; } = "SUBMITTED";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("reviewed_by")]
    public string? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }
}

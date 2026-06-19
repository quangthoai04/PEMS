using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Feedbacks;

[Table("feedbacks")]
public class Feedback
{
    [Key]
    [Column("feedback_id")]
    public ulong FeedbackId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong? VisitInstanceId { get; set; }

    [Column("submitted_by_user_id")]
    public ulong? SubmittedByUserId { get; set; }

    [Column("submitter_role")]
    public string SubmitterRole { get; set; } = null!;

    [Column("submitter_context")]
    public string SubmitterContext { get; set; } = string.Empty;

    [Column("submitter_name_snapshot")]
    public string SubmitterNameSnapshot { get; set; } = null!;

    [Column("target_user_id")]
    public ulong? TargetUserId { get; set; }

    [Column("target_role")]
    public string TargetRole { get; set; } = null!;

    [Column("target_context")]
    public string TargetContext { get; set; } = string.Empty;

    [Column("target_name_snapshot")]
    public string TargetNameSnapshot { get; set; } = null!;

    [Column("rating")]
    public byte Rating { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }
}

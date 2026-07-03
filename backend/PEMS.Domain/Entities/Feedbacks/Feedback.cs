using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Feedbacks;

/// <summary>
/// Feedback rule mới (pems_full_v10 feedback_rule_fixed):
/// - VISITOR_OVERALL: Visitor đánh giá chung chuyến thăm (target VISIT_REQUEST/VISIT_INSTANCE, target_user_id NULL).
/// - HOST_PARTICIPANT: Host đánh giá bên tham gia (VISIT_PARTICIPANT / GUEST_MEMBER / USER).
/// - HOST_LOGISTICS: Host đánh giá bên cho mượn đồ/hậu cần (LOGISTICS_ITEM / LOGISTICS_HANDOVER / DEPARTMENT / USER).
/// Rating 1..5 bắt buộc; comment nullable. Target mapping được validate ở backend (FeedbackRules).
/// </summary>
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

    [Column("feedback_type")]
    public string FeedbackType { get; set; } = null!; // VISITOR_OVERALL | HOST_PARTICIPANT | HOST_LOGISTICS

    [Column("submitted_by_user_id")]
    public ulong SubmittedByUserId { get; set; }

    [Column("submitter_role")]
    public string SubmitterRole { get; set; } = null!; // VISITOR | HOST

    [Column("submitter_context")]
    public string SubmitterContext { get; set; } = string.Empty;

    [Column("submitter_name_snapshot")]
    public string SubmitterNameSnapshot { get; set; } = null!;

    [Column("target_type")]
    public string TargetType { get; set; } = null!; // VISIT_REQUEST | VISIT_INSTANCE | VISIT_PARTICIPANT | GUEST_MEMBER | LOGISTICS_ITEM | LOGISTICS_HANDOVER | USER | DEPARTMENT

    [Column("target_user_id")]
    public ulong? TargetUserId { get; set; }

    [Column("target_participant_id")]
    public ulong? TargetParticipantId { get; set; }

    [Column("target_guest_member_id")]
    public ulong? TargetGuestMemberId { get; set; }

    [Column("target_logistics_item_id")]
    public ulong? TargetLogisticsItemId { get; set; }

    [Column("target_handover_id")]
    public ulong? TargetHandoverId { get; set; }

    [Column("target_department_id")]
    public ulong? TargetDepartmentId { get; set; }

    [Column("target_role")]
    public string? TargetRole { get; set; }

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

    public virtual ICollection<FeedbackRatingItem> RatingItems { get; set; } = new List<FeedbackRatingItem>();
}

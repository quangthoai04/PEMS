using PEMS.Domain.Entities.AgendaTemplates;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Faqs;
using PEMS.Domain.Entities.Feedbacks;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Minutes;
using PEMS.Domain.Entities.News;
using PEMS.Domain.Entities.Notifications;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Reports;
using PEMS.Domain.Entities.Tasks;
using PEMS.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Feedbacks;

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

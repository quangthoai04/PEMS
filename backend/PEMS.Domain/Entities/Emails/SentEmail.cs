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

namespace PEMS.Domain.Entities.Emails;

[Table("sent_emails")]
public class SentEmail
{
    [Key]
    [Column("email_id")]
    public string EmailId { get; set; } = null!;

    [Column("program")]
    public string? Program { get; set; }

    [Column("visit_id")]
    public string? VisitId { get; set; }

    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Column("body")]
    public string? Body { get; set; }

    [Column("sender_user_id")]
    public string? SenderUserId { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("send_time")]
    public DateTime? SendTime { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Dang xu ly";

    [Column("has_new_reply")]
    public bool HasNewReply { get; set; }

    public virtual ICollection<SentEmailRecipient> Recipients { get; set; } = new List<SentEmailRecipient>();
}

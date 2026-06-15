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

[Table("sent_email_recipients")]
public class SentEmailRecipient
{
    [Key]
    [Column("recipient_id")]
    public string RecipientId { get; set; } = null!;

    [Column("email_id")]
    public string EmailId { get; set; } = null!;

    [Column("email")]
    public string Email { get; set; } = null!;

    [Column("name")]
    public string? Name { get; set; }

    [Column("partner_contact_id")]
    public string? PartnerContactId { get; set; }

    [Column("delivery_status")]
    public string DeliveryStatus { get; set; } = "Dang xu ly";

    public virtual SentEmail SentEmail { get; set; } = null!;
}

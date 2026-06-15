using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

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

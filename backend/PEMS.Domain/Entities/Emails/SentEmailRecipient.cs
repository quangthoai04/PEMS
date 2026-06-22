using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Emails;

[Table("sent_email_recipients")]
public class SentEmailRecipient
{
    [Key]
    [Column("sent_email_recipient_id")]
    public ulong SentEmailRecipientId { get; set; }

    [Column("sent_email_id")]
    public ulong SentEmailId { get; set; }

    [Column("recipient_email")]
    public string RecipientEmail { get; set; } = null!;

    [Column("recipient_name")]
    public string? RecipientName { get; set; }

    [Column("recipient_type")]
    public string RecipientType { get; set; } = "TO";

    [Column("delivery_status")]
    public string DeliveryStatus { get; set; } = "QUEUED";

    [Column("provider_message_id")]
    public string? ProviderMessageId { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual SentEmail SentEmail { get; set; } = null!;
}

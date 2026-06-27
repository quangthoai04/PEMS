using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Enums;

namespace PEMS.Domain.Entities.Emails;

[Table("sent_emails")]
public class SentEmail
{
    [Key]
    [Column("sent_email_id")]
    public ulong SentEmailId { get; set; }

    [Column("email_template_id")]
    public ulong? EmailTemplateId { get; set; }

    [Column("related_type")]
    public string? RelatedType { get; set; }

    [Column("related_id")]
    public ulong? RelatedId { get; set; }

    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Column("body_snapshot")]
    public string? BodySnapshot { get; set; }

    /// <summary>SQL: body_format ENUM('PLAIN_TEXT','HTML') NOT NULL DEFAULT 'HTML'.</summary>
    [Column("body_format")]
    public EmailBodyFormat BodyFormat { get; set; } = EmailBodyFormat.HTML;

    [Column("provider_thread_id")]
    public string? ProviderThreadId { get; set; }

    [Column("provider_message_id")]
    public string? ProviderMessageId { get; set; }

    [Column("retry_count")]
    public uint RetryCount { get; set; }

    [Column("last_attempt_at")]
    public DateTime? LastAttemptAt { get; set; }

    [Column("status")]
    public string Status { get; set; } = "QUEUED";

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("sent_by")]
    public ulong? SentBy { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual EmailTemplate? EmailTemplate { get; set; }
    public virtual ICollection<SentEmailRecipient> Recipients { get; set; } = new List<SentEmailRecipient>();
    public virtual ICollection<SentEmailAttachment> Attachments { get; set; } = new List<SentEmailAttachment>();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Emails;

[Table("sent_emails")]
public class SentEmail
{
    [Key]
    [Column("sent_email_id")]
    public string SentEmailId { get; set; } = null!;

    [Column("email_template_id")]
    public string? EmailTemplateId { get; set; }

    [Column("related_type")]
    public string? RelatedType { get; set; }

    [Column("related_id")]
    public string? RelatedId { get; set; }

    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Column("body_snapshot")]
    public string? BodySnapshot { get; set; }

    [Column("recipients_json")]
    public string? RecipientsJson { get; set; }

    [Column("metadata_json")]
    public string? MetadataJson { get; set; }

    [Column("status")]
    public string Status { get; set; } = "QUEUED";

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("sent_by")]
    public string? SentBy { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual EmailTemplate? EmailTemplate { get; set; }
}

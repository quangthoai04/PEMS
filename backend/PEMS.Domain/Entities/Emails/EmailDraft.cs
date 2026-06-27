using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Enums;

namespace PEMS.Domain.Entities.Emails;

/// <summary>
/// An editable email draft / autosave before sending, similar to a mail-compose draft. Never
/// hard-deleted: it moves to SENT (linked to the produced <see cref="SentEmail"/>) or DISCARDED.
/// Maps SQL table <c>email_drafts</c>.
/// </summary>
[Table("email_drafts")]
public class EmailDraft
{
    [Key]
    [Column("email_draft_id")]
    public ulong EmailDraftId { get; set; }

    [Column("email_template_id")]
    public ulong? EmailTemplateId { get; set; }

    [Column("related_type")]
    public string? RelatedType { get; set; }

    [Column("related_id")]
    public ulong? RelatedId { get; set; }

    [Column("subject")]
    public string? Subject { get; set; }

    /// <summary>SQL column is <c>body_content</c> (not <c>body</c>).</summary>
    [Column("body_content")]
    public string? BodyContent { get; set; }

    [Column("body_format")]
    public EmailBodyFormat BodyFormat { get; set; } = EmailBodyFormat.HTML;

    [Column("status")]
    public EmailDraftStatus Status { get; set; } = EmailDraftStatus.DRAFT;

    [Column("sent_email_id")]
    public ulong? SentEmailId { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("last_edited_by")]
    public ulong? LastEditedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("discarded_at")]
    public DateTime? DiscardedAt { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    public virtual EmailTemplate? EmailTemplate { get; set; }
    public virtual SentEmail? SentEmail { get; set; }
    public virtual ICollection<EmailDraftRecipient> Recipients { get; set; } = new List<EmailDraftRecipient>();
    public virtual ICollection<EmailDraftAttachment> Attachments { get; set; } = new List<EmailDraftAttachment>();
}

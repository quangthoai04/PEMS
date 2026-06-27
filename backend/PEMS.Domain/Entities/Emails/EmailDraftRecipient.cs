using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Emails;

/// <summary>
/// A recipient (TO/CC/BCC) of an editable email draft. Maps SQL table
/// <c>email_draft_recipients</c>. recipient_type is kept as the raw ENUM string for parity with
/// <see cref="SentEmailRecipient"/>.
/// </summary>
[Table("email_draft_recipients")]
public class EmailDraftRecipient
{
    [Key]
    [Column("email_draft_recipient_id")]
    public ulong EmailDraftRecipientId { get; set; }

    [Column("email_draft_id")]
    public ulong EmailDraftId { get; set; }

    [Column("recipient_email")]
    public string RecipientEmail { get; set; } = null!;

    [Column("recipient_name")]
    public string? RecipientName { get; set; }

    [Column("recipient_type")]
    public string RecipientType { get; set; } = "TO";

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual EmailDraft EmailDraft { get; set; } = null!;
}

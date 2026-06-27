using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Enums;

namespace PEMS.Domain.Entities.Emails;

/// <summary>
/// A file/image attached to an email draft before sending. The binary lives in <c>files</c>;
/// this row only references it. Maps SQL table <c>email_draft_attachments</c>.
/// </summary>
[Table("email_draft_attachments")]
public class EmailDraftAttachment
{
    [Key]
    [Column("email_draft_attachment_id")]
    public ulong EmailDraftAttachmentId { get; set; }

    [Column("email_draft_id")]
    public ulong EmailDraftId { get; set; }

    [Column("file_id")]
    public ulong FileId { get; set; }

    [Column("attachment_type")]
    public EmailAttachmentType AttachmentType { get; set; } = EmailAttachmentType.ATTACHMENT;

    [Column("content_id")]
    public string? ContentId { get; set; }

    [Column("display_name")]
    public string? DisplayName { get; set; }

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual EmailDraft EmailDraft { get; set; } = null!;
    public virtual UploadedFile File { get; set; } = null!;
}

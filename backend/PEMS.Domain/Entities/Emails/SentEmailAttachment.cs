using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Enums;

namespace PEMS.Domain.Entities.Emails;

/// <summary>
/// A file/image attached to a sent email. The binary lives in <c>files</c> / external storage;
/// this row only references it. INLINE_IMAGE rows carry a Content-ID so the HTML body can embed
/// them via <c>&lt;img src="cid:..."&gt;</c>. Maps SQL table <c>sent_email_attachments</c>.
/// </summary>
[Table("sent_email_attachments")]
public class SentEmailAttachment
{
    [Key]
    [Column("sent_email_attachment_id")]
    public ulong SentEmailAttachmentId { get; set; }

    [Column("sent_email_id")]
    public ulong SentEmailId { get; set; }

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

    public virtual SentEmail SentEmail { get; set; } = null!;
    public virtual UploadedFile File { get; set; } = null!;
}

namespace PEMS.Application.Emails.Common;

/// <summary>
/// The two shapes a composed message carries: who it goes to, and what is attached.
///
/// <para>
/// They were the input to <c>email_drafts</c> and are now the input to a send. The names changed with
/// the storage — nothing here was ever about a draft, which is why the participant-invite and
/// logistics-request paths were already using them without one.
/// </para>
/// </summary>

/// <summary>A TO/CC/BCC recipient line on a composed message.</summary>
public sealed class EmailComposeRecipientInput
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    /// <summary>TO | CC | BCC (defaults to TO).</summary>
    public string? RecipientType { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>A file/inline-image attachment on a composed message. Only references a file_id.</summary>
public sealed class EmailComposeAttachmentInput
{
    public ulong FileId { get; set; }
    /// <summary>ATTACHMENT | INLINE_IMAGE (defaults to ATTACHMENT).</summary>
    public string? AttachmentType { get; set; }
    /// <summary>Required when AttachmentType = INLINE_IMAGE (the cid the HTML body references).</summary>
    public string? ContentId { get; set; }
    public string? DisplayName { get; set; }
    public int DisplayOrder { get; set; }
}

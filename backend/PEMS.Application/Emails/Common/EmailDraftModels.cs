using System;
using System.Collections.Generic;

namespace PEMS.Application.Emails.Common;

// ── Inputs (Create / Update / Send draft) ─────────────────────────────────────

/// <summary>A TO/CC/BCC recipient line on an email draft.</summary>
public sealed class EmailDraftRecipientInput
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    /// <summary>TO | CC | BCC (defaults to TO).</summary>
    public string? RecipientType { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>A file/inline-image attachment on an email draft. Only references a file_id.</summary>
public sealed class EmailDraftAttachmentInput
{
    public ulong FileId { get; set; }
    /// <summary>ATTACHMENT | INLINE_IMAGE (defaults to ATTACHMENT).</summary>
    public string? AttachmentType { get; set; }
    /// <summary>Required when AttachmentType = INLINE_IMAGE (the cid the HTML body references).</summary>
    public string? ContentId { get; set; }
    public string? DisplayName { get; set; }
    public int DisplayOrder { get; set; }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

public sealed class EmailDraftDto
{
    public ulong EmailDraftId { get; set; }
    public ulong? EmailTemplateId { get; set; }
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public string? Subject { get; set; }
    public string? BodyContent { get; set; }
    /// <summary>PLAIN_TEXT | HTML.</summary>
    public string BodyFormat { get; set; } = "HTML";
    /// <summary>DRAFT | SENT | DISCARDED.</summary>
    public string Status { get; set; } = "DRAFT";
    public ulong? SentEmailId { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }

    /// <summary>
    /// Every recipient in one list, each carrying its type. Kept for callers that group the list
    /// themselves; <see cref="To"/>/<see cref="Cc"/>/<see cref="Bcc"/> are the same rows pre-grouped.
    /// </summary>
    public List<EmailDraftRecipientDto> Recipients { get; set; } = new();

    /// <summary>Visible primary recipients, in the order they were entered.</summary>
    public List<EmailDraftRecipientDto> To { get; set; } = new();

    /// <summary>Carbon copies.</summary>
    public List<EmailDraftRecipientDto> Cc { get; set; } = new();

    /// <summary>
    /// Blind copies. Returned in full because a draft is only ever readable by its own author — the person
    /// who chose them. The equivalent list on a SENT message is filtered per viewer (see
    /// <c>SentEmailAccess</c>); these are not the same situation and must not be given the same rule.
    /// </summary>
    public List<EmailDraftRecipientDto> Bcc { get; set; } = new();

    public List<EmailDraftAttachmentDto> Attachments { get; set; } = new();
}

public sealed class EmailDraftRecipientDto
{
    public ulong EmailDraftRecipientId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string RecipientType { get; set; } = "TO";
    public int DisplayOrder { get; set; }
}

public sealed class EmailDraftAttachmentDto
{
    public ulong EmailDraftAttachmentId { get; set; }
    public ulong FileId { get; set; }
    public string AttachmentType { get; set; } = "ATTACHMENT";
    public string? ContentId { get; set; }
    public string? DisplayName { get; set; }
    public int DisplayOrder { get; set; }
    public string? OriginalFilename { get; set; }
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public string? WebViewUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}

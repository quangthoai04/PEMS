using System;
using System.Collections.Generic;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Queries.ViewEmail;

public class ViewEmailDto
{
    public ulong SentEmailId { get; set; }
    public ulong? EmailTemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string? TemplateCode { get; set; }
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public string Subject { get; set; } = null!;
    public string BodySnapshot { get; set; } = null!;

    /// <summary>
    /// PLAIN_TEXT or HTML — how <see cref="BodySnapshot"/> is meant to be displayed.
    ///
    /// <para>
    /// The history screen needs it to decide whether to preserve the source's line breaks. A plain-text
    /// body depends on them; an HTML body does not, and rendering one under <c>white-space: pre-wrap</c>
    /// turns every newline between tags into a visible gap, which inside a table reads as a broken
    /// layout. Without this field the screen had to guess, and it guessed the same way for both.
    /// </para>
    /// </summary>
    public string BodyFormat { get; set; } = nameof(EmailBodyFormat.HTML);
    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
    public uint RetryCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Whether this viewer may reply to this message — see <see cref="Common.SentEmailAccess.CanOfferReply"/>.
    /// The screen used to read a <c>canReply</c> that no detail response ever carried, so the reply
    /// affordance was permanently hidden; the value is computed here rather than guessed on the client,
    /// because only the server knows the viewer's relation to the envelope.
    /// </summary>
    public bool CanReply { get; set; }

    /// <summary>
    /// Whether this viewer may close the message off ("đánh dấu đã xử lý"). Same predicate the command
    /// enforces (<see cref="PEMS.Application.Emails.Common.SentEmailAccess.CanMarkComplete"/>), so the
    /// button appears exactly when pressing it would work.
    /// </summary>
    public bool CanMarkComplete { get; set; }

    public SentEmailSenderDto? Sender { get; set; }
    public List<SentEmailRecipientDto> Recipients { get; set; } = new();
    public List<SentEmailAttachmentDto> Attachments { get; set; } = new();
}

public class SentEmailSenderDto
{
    public ulong? UserId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
}

public class SentEmailRecipientDto
{
    public string RecipientEmail { get; set; } = null!;
    public string? RecipientName { get; set; }
    public string RecipientType { get; set; } = null!;
    public string DeliveryStatus { get; set; } = null!;
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}

public class SentEmailAttachmentDto
{
    public ulong FileId { get; set; }
    public string FileName { get; set; } = null!;
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public bool IsInline { get; set; }
    public string? ContentId { get; set; }
    public string? PreviewUrl { get; set; }
    public string? DownloadUrl { get; set; }
}
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceSentEmails;

public sealed class GetVisitInstanceSentEmailsResponse
{
    public List<SentEmailHistoryDto> Items { get; init; } = new();
}

/// <summary>One sent_emails row + its recipients (newest first in the list).</summary>
public sealed class SentEmailHistoryDto
{
    public ulong SentEmailId { get; set; }
    public string? TemplateCode { get; set; }
    public string? TemplateName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? BodySnapshot { get; set; }
    /// <summary>PLAIN_TEXT | HTML — how BodySnapshot should be rendered.</summary>
    public string BodyFormat { get; set; } = "HTML";
    /// <summary>QUEUED | SENT | PARTIAL_FAILED | FAILED | DELIVERED.</summary>
    public string EmailStatus { get; set; } = string.Empty;
    public string? SentByName { get; set; }
    public string? SentAt { get; set; }         // "yyyy-MM-ddTHH:mm:ss" wall-clock
    public string? DeliveredAt { get; set; }
    public string? CreatedAt { get; set; }
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public List<SentEmailRecipientDto> Recipients { get; set; } = new();
    public List<SentEmailAttachmentDto> Attachments { get; set; } = new();
}

public sealed class SentEmailRecipientDto
{
    public string? RecipientName { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientType { get; set; } = "TO";
    public string DeliveryStatus { get; set; } = string.Empty;
    public string? SentAt { get; set; }
    public string? DeliveredAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>One sent_email_attachments row + the referenced file's metadata.</summary>
public sealed class SentEmailAttachmentDto
{
    public ulong SentEmailAttachmentId { get; set; }
    public ulong FileId { get; set; }
    /// <summary>ATTACHMENT | INLINE_IMAGE.</summary>
    public string AttachmentType { get; set; } = "ATTACHMENT";
    public string? ContentId { get; set; }
    public string? DisplayName { get; set; }
    public string? OriginalFilename { get; set; }
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public string? WebViewUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}

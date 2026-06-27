using System;
using System.Collections.Generic;

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
    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
    public uint RetryCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }

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
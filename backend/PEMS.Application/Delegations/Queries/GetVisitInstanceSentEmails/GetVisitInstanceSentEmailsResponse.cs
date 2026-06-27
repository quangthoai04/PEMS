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
    /// <summary>QUEUED | SENT | FAILED | DELIVERED.</summary>
    public string EmailStatus { get; set; } = string.Empty;
    public string? SentByName { get; set; }
    public string? SentAt { get; set; }         // "yyyy-MM-ddTHH:mm:ss" wall-clock
    public string? DeliveredAt { get; set; }
    public string? CreatedAt { get; set; }
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public List<SentEmailRecipientDto> Recipients { get; set; } = new();
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

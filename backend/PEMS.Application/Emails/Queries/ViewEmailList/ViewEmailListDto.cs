using PEMS.Application.Common.Models;

namespace PEMS.Application.Emails.Queries.ViewEmailList;

public class ViewEmailListResponse
{
    public IReadOnlyList<EmailListItemDto> Items { get; set; } = new List<EmailListItemDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class EmailListItemDto
{
    public ulong Id { get; set; }
    public string SourceType { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string? Snippet { get; set; }
    public string? CounterpartName { get; set; }
    public string? CounterpartEmail { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SendStatus { get; set; } = null!;
    public string? DeliveryStatus { get; set; }
    public string ProcessStatus { get; set; } = null!;
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public bool CanReply { get; set; }
    public bool CanConfirm { get; set; }
    public bool CanMarkComplete { get; set; }
}

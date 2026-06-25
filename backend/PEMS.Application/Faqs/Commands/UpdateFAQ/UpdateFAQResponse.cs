namespace PEMS.Application.Faqs.Commands.UpdateFAQ;

public sealed class UpdateFAQResponse
{
    public ulong FaqId { get; init; }
    public string FaqType { get; init; } = string.Empty;
    public string FaqTypeLabel { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public ulong? CreatedBy { get; init; }
    public string? CreatedByName { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public ulong? UpdatedBy { get; init; }
    public string? UpdatedByName { get; init; }
    public bool Changed { get; init; }
}

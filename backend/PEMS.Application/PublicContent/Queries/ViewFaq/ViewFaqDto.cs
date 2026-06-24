namespace PEMS.Application.PublicContent.Queries.ViewFAQ;

public sealed class ViewFaqDto
{
    public ulong FaqId { get; init; }
    public string FaqType { get; init; } = default!;
    public string FaqTypeLabel { get; init; } = default!;
    public string Question { get; init; } = default!;
    public string Answer { get; init; } = default!;
    public int DisplayOrder { get; init; }
    public DateTime CreatedAt { get; init; }
}

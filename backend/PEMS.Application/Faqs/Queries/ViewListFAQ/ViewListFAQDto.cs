namespace PEMS.Application.Faqs.Queries.ViewListFAQ;

public sealed class ViewListFAQDto
{
    public ulong FaqId { get; init; }

    public string FaqType { get; init; } = default!;
    public string FaqTypeLabel { get; init; } = default!;

    public string Question { get; init; } = default!;
    public string Answer { get; init; } = default!;
    public string? EnglishQuestion { get; init; }
    public string? EnglishAnswer { get; init; }
    public bool HasEnglishTranslation { get; init; }

    public int DisplayOrder { get; init; }

    public string Status { get; init; } = default!;
    public string StatusLabel { get; init; } = default!;

    public DateTime CreatedAt { get; init; }
    public ulong? CreatedBy { get; init; }
    public string? CreatedByName { get; init; }

    public DateTime? UpdatedAt { get; init; }
    public ulong? UpdatedBy { get; init; }
    public string? UpdatedByName { get; init; }
}

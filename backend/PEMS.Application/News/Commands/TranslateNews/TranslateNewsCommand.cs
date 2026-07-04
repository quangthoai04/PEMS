using MediatR;

namespace PEMS.Application.News.Commands.TranslateNews;

/// <summary>
/// Auto-translates a news post's translation into a new language using the configured
/// machine-translation provider. With <see cref="Save"/> = false the translated content
/// is only returned (preview / manual adjustment before saving); with true the result is
/// persisted as a new translation (sections + copied image mappings) in one step.
/// </summary>
public sealed record TranslateNewsCommand : IRequest<TranslateNewsResponse>
{
    public ulong NewsId { get; init; }
    public string SourceLanguage { get; init; } = "vi";
    public string TargetLanguage { get; init; } = string.Empty;
    public bool Save { get; init; }
}

public sealed class TranslateNewsResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public ulong NewsId { get; init; }
    public string SourceLanguage { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public IReadOnlyList<TranslatedNewsSectionDto> Sections { get; init; }
        = Array.Empty<TranslatedNewsSectionDto>();
    public bool Saved { get; init; }
    public ulong? TranslationId { get; init; }
}

public sealed class TranslatedNewsSectionDto
{
    public int SectionOrder { get; init; }
    public string SectionTitle { get; init; } = string.Empty;
    public string SectionBodyHtml { get; init; } = string.Empty;
}

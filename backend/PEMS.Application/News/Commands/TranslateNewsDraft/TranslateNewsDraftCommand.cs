using MediatR;

namespace PEMS.Application.News.Commands.TranslateNewsDraft;

/// <summary>
/// Translates a News post that does not exist yet (still being composed in the create-news
/// form, so there is no NewsId to call <c>TranslateNewsCommand</c> with). Preview-only — never
/// persists anything; the caller decides what to do with the translated text (fill the English
/// column, let the user edit further, then submit the whole bilingual post via
/// <c>CreateNewsCommand</c>). Mirrors <c>TranslateNewsCommandHandler</c>'s batching/sanitizing
/// logic exactly, just without the NewsId/ownership lookup.
/// </summary>
public sealed record TranslateNewsDraftCommand : IRequest<TranslateNewsDraftResponse>
{
    public string SourceLanguage { get; init; } = "vi";
    public string TargetLanguage { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public IReadOnlyList<TranslateNewsDraftSectionDto> Sections { get; init; }
        = Array.Empty<TranslateNewsDraftSectionDto>();
}

public sealed record TranslateNewsDraftSectionDto
{
    public int SectionOrder { get; init; }
    public string SectionTitle { get; init; } = string.Empty;
    public string SectionBodyHtml { get; init; } = string.Empty;
}

public sealed class TranslateNewsDraftResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = string.Empty;
    public string TargetLanguage { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public IReadOnlyList<TranslatedNewsDraftSectionDto> Sections { get; init; }
        = Array.Empty<TranslatedNewsDraftSectionDto>();
}

public sealed class TranslatedNewsDraftSectionDto
{
    public int SectionOrder { get; init; }
    public string SectionTitle { get; init; } = string.Empty;
    public string SectionBodyHtml { get; init; } = string.Empty;
}

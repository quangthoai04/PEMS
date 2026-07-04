namespace PEMS.Application.News.Services;

/// <summary>
/// Machine-translation provider used by the News auto-translate use case.
/// Implementations must preserve HTML tags/attributes for
/// <see cref="TranslateHtmlAsync"/> (Google Cloud Translation supports
/// mimeType text/html natively) and never log credentials or payloads.
/// </summary>
public interface INewsTranslationService
{
    /// <summary>Translates plain-text segments; result order matches input order.</summary>
    Task<IReadOnlyList<string>> TranslateTextAsync(
        IReadOnlyList<string> contents,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken);

    /// <summary>Translates HTML segments keeping markup intact; result order matches input order.</summary>
    Task<IReadOnlyList<string>> TranslateHtmlAsync(
        IReadOnlyList<string> contents,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken);
}

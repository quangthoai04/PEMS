namespace PEMS.Application.Translation;

/// <summary>
/// Shared machine-translation abstraction (Google Cloud Translation v3 behind the scenes) used by any
/// module that persists translated content (Gallery names/titles; News keeps its own module-scoped
/// <c>INewsTranslationService</c> facade over the same single provider — there is intentionally only ONE
/// Google provider/credential pipeline). Implementations must keep result order aligned with input order
/// and never log credentials or payloads.
/// </summary>
public interface IContentTranslationService
{
    /// <summary>Translates plain-text segments in one batch request; result order matches input order.</summary>
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

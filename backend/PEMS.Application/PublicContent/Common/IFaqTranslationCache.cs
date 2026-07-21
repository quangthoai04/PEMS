namespace PEMS.Application.PublicContent.Common;

/// <summary>
/// Runtime translation + cache for FAQ question/answer text. The <c>faqs</c> table intentionally
/// stores Vietnamese only (language_code was removed by design — see database changelog); English
/// (and any other supported language) is produced on demand via the existing News translation
/// provider (<see cref="PEMS.Application.News.Services.INewsTranslationService"/>, generic despite
/// the name) and cached so repeat requests don't re-call Google Translate. Shared by
/// <c>ViewFaqQueryHandler</c> and <c>SearchInformationQueryHandler</c> so both honor the caller's
/// languageCode without duplicating the batching/caching logic.
/// </summary>
public interface IFaqTranslationCache
{
    /// <summary>
    /// Translates (question, answer) pairs for the given FAQ rows into <paramref name="languageCode"/>,
    /// batching every uncached row into a single provider call. Returns the Vietnamese originals
    /// unchanged when <paramref name="languageCode"/> is "vi" or unsupported/empty.
    /// </summary>
    Task<IReadOnlyDictionary<ulong, (string Question, string Answer)>> TranslateAsync(
        IReadOnlyList<FaqTranslationSource> faqs,
        string? languageCode,
        CancellationToken cancellationToken);
}

/// <summary>One FAQ row's Vietnamese source text + the cache-busting version stamp.</summary>
public sealed record FaqTranslationSource(ulong FaqId, string Question, string Answer, DateTime? UpdatedAt);

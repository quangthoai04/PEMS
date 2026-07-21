namespace PEMS.Application.Partners.Common;

/// <summary>
/// Runtime translation + cache for a partner's <c>description</c> text. Like FAQ, the
/// <c>partners</c> table stores Vietnamese only; English (and any other supported language) is
/// produced on demand via the shared News translation provider
/// (<see cref="PEMS.Application.News.Services.INewsTranslationService"/>, generic despite the
/// name) and cached so repeat requests don't re-call the translation API. Mirrors
/// <c>PEMS.Application.PublicContent.Common.IFaqTranslationCache</c>'s batching/caching shape,
/// specialized to a single text field instead of a question/answer pair.
/// </summary>
public interface IPartnerDescriptionTranslationCache
{
    /// <summary>
    /// Translates each partner's description into <paramref name="languageCode"/>, batching every
    /// uncached row into a single provider call. Returns the Vietnamese originals unchanged when
    /// <paramref name="languageCode"/> is "vi", empty, or unsupported. Partners with a null/empty
    /// description are omitted from the returned map (nothing to translate).
    /// </summary>
    Task<IReadOnlyDictionary<ulong, string>> TranslateAsync(
        IReadOnlyList<PartnerDescriptionTranslationSource> partners,
        string? languageCode,
        CancellationToken cancellationToken);
}

/// <summary>One partner row's Vietnamese description + the cache-busting version stamp.</summary>
public sealed record PartnerDescriptionTranslationSource(ulong PartnerId, string? Description, DateTime? UpdatedAt);

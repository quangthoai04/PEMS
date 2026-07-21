using Microsoft.Extensions.Caching.Memory;
using PEMS.Application.News.Services;
using PEMS.Domain.Constants;

namespace PEMS.Application.Partners.Common;

public sealed class PartnerDescriptionTranslationCache : IPartnerDescriptionTranslationCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly INewsTranslationService _translator;
    private readonly IMemoryCache _cache;

    public PartnerDescriptionTranslationCache(INewsTranslationService translator, IMemoryCache cache)
    {
        _translator = translator;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<ulong, string>> TranslateAsync(
        IReadOnlyList<PartnerDescriptionTranslationSource> partners,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<ulong, string>();

        var withDescription = partners.Where(p => !string.IsNullOrWhiteSpace(p.Description)).ToList();
        if (withDescription.Count == 0)
            return result;

        var targetLang = languageCode?.Trim();
        var isDefaultLanguage = string.IsNullOrWhiteSpace(targetLang)
            || string.Equals(targetLang, NewsConstants.Languages.Default, StringComparison.OrdinalIgnoreCase);

        if (isDefaultLanguage)
        {
            foreach (var p in withDescription)
                result[p.PartnerId] = p.Description!;
            return result;
        }

        // Unsupported language codes fall back to the Vietnamese original rather than failing
        // the whole page — mirrors FaqTranslationCache's behavior.
        if (!NewsConstants.Languages.Allowed.Contains(targetLang!))
        {
            foreach (var p in withDescription)
                result[p.PartnerId] = p.Description!;
            return result;
        }

        var uncached = new List<PartnerDescriptionTranslationSource>();
        foreach (var p in withDescription)
        {
            var cacheKey = BuildCacheKey(p, targetLang!);
            if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
                result[p.PartnerId] = cached;
            else
                uncached.Add(p);
        }

        if (uncached.Count == 0)
            return result;

        var batch = uncached.Select(p => p.Description!).ToList();
        var translated = await _translator.TranslateTextAsync(
            batch, NewsConstants.Languages.Default, targetLang!, cancellationToken);

        for (var i = 0; i < uncached.Count; i++)
        {
            var p = uncached[i];
            var description = translated[i];

            result[p.PartnerId] = description;

            var cacheKey = BuildCacheKey(p, targetLang!);
            _cache.Set(cacheKey, description, CacheTtl);
        }

        return result;
    }

    /// <summary>
    /// Includes the partner's UpdatedAt tick stamp so an admin edit naturally invalidates the old
    /// cached translation (new key → cache miss → re-translate) without explicit eviction.
    /// </summary>
    private static string BuildCacheKey(PartnerDescriptionTranslationSource partner, string languageCode) =>
        $"partner-desc-translate:{partner.PartnerId}:{languageCode}:{partner.UpdatedAt?.Ticks ?? 0}";
}

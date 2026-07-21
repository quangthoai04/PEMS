using Microsoft.Extensions.Caching.Memory;
using PEMS.Application.News.Services;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Common;

public sealed class FaqTranslationCache : IFaqTranslationCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly INewsTranslationService _translator;
    private readonly IMemoryCache _cache;

    public FaqTranslationCache(INewsTranslationService translator, IMemoryCache cache)
    {
        _translator = translator;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<ulong, (string Question, string Answer)>> TranslateAsync(
        IReadOnlyList<FaqTranslationSource> faqs,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<ulong, (string Question, string Answer)>();

        if (faqs.Count == 0)
            return result;

        var targetLang = languageCode?.Trim();
        var isDefaultLanguage = string.IsNullOrWhiteSpace(targetLang)
            || string.Equals(targetLang, NewsConstants.Languages.Default, StringComparison.OrdinalIgnoreCase);

        if (isDefaultLanguage)
        {
            foreach (var faq in faqs)
                result[faq.FaqId] = (faq.Question, faq.Answer);
            return result;
        }

        // Unsupported language codes fall back to the Vietnamese original rather than failing
        // the whole page — mirrors FaqConstants.ToTypeLabel's "default to Vietnamese" behavior.
        if (!NewsConstants.Languages.Allowed.Contains(targetLang!))
        {
            foreach (var faq in faqs)
                result[faq.FaqId] = (faq.Question, faq.Answer);
            return result;
        }

        var uncached = new List<FaqTranslationSource>();
        foreach (var faq in faqs)
        {
            var cacheKey = BuildCacheKey(faq, targetLang!);
            if (_cache.TryGetValue(cacheKey, out (string Question, string Answer) cached))
                result[faq.FaqId] = cached;
            else
                uncached.Add(faq);
        }

        if (uncached.Count == 0)
            return result;

        // Batch every uncached row into one provider call: [q1, a1, q2, a2, ...].
        var batch = new List<string>(uncached.Count * 2);
        foreach (var faq in uncached)
        {
            batch.Add(faq.Question);
            batch.Add(faq.Answer);
        }

        var translated = await _translator.TranslateTextAsync(
            batch, NewsConstants.Languages.Default, targetLang!, cancellationToken);

        for (var i = 0; i < uncached.Count; i++)
        {
            var faq = uncached[i];
            var question = translated[i * 2];
            var answer = translated[i * 2 + 1];

            result[faq.FaqId] = (question, answer);

            var cacheKey = BuildCacheKey(faq, targetLang!);
            _cache.Set(cacheKey, (question, answer), CacheTtl);
        }

        return result;
    }

    /// <summary>
    /// Includes the FAQ's UpdatedAt tick stamp so an admin edit naturally invalidates the old
    /// cached translation (new key → cache miss → re-translate) without explicit eviction.
    /// </summary>
    private static string BuildCacheKey(FaqTranslationSource faq, string languageCode) =>
        $"faq-translate:{faq.FaqId}:{languageCode}:{faq.UpdatedAt?.Ticks ?? 0}";
}

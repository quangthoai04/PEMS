using Microsoft.Extensions.Logging;
using PEMS.Application.Translation;

namespace PEMS.Application.Galleries.Common;

/// <summary>One string to auto-translate: the ALREADY-normalized Vietnamese source + its EN column cap.</summary>
public sealed class GalleryTranslationRequest
{
    public GalleryTranslationRequest(string normalizedSource, int maxLength)
    {
        NormalizedSource = normalizedSource;
        MaxLength = maxLength;
    }

    public string NormalizedSource { get; }
    public int MaxLength { get; }
}

/// <summary>
/// Per-source outcome. <see cref="SourceHash"/> is ALWAYS computed (hash of the new Vietnamese source is
/// stored even when translation fails, so a later retry/backfill knows which source the FAILED state
/// belongs to). <see cref="TranslatedText"/> is non-null only when <see cref="Success"/>.
/// </summary>
public sealed class GalleryTranslationResult
{
    public string SourceText { get; init; } = string.Empty;
    public string SourceHash { get; init; } = string.Empty;
    public string? TranslatedText { get; init; }
    public bool Success { get; init; }
    /// <summary>READY when success, FAILED otherwise — ready to persist to translation_status.</summary>
    public string Status => Success ? GalleryTranslationStatuses.Ready : GalleryTranslationStatuses.Failed;
}

/// <summary>
/// The single seam between the Gallery write path and the translation provider. Batches every string of
/// one business action into ONE provider request (deduplicated), maps results back in input order, and
/// NEVER throws for provider problems (missing config, HTTP error, timeout, quota, bad payload) — those
/// come back as per-source FAILED results so the Vietnamese save always proceeds (BR §3.3). A translated
/// string that is blank or exceeds its EN column cap is also FAILED (never truncated, BR §23). Only a
/// caller-initiated cancellation propagates.
/// </summary>
public interface IGalleryTranslationCoordinator
{
    /// <summary>Translates VI → EN. Result list is aligned index-by-index with <paramref name="requests"/>.</summary>
    Task<IReadOnlyList<GalleryTranslationResult>> TranslateAsync(
        IReadOnlyList<GalleryTranslationRequest> requests, CancellationToken cancellationToken);
}

public sealed class GalleryTranslationCoordinator : IGalleryTranslationCoordinator
{
    private const string SourceLanguage = "vi";
    private const string TargetLanguage = "en";

    private readonly IContentTranslationService _translation;
    private readonly ILogger<GalleryTranslationCoordinator> _logger;

    public GalleryTranslationCoordinator(
        IContentTranslationService translation,
        ILogger<GalleryTranslationCoordinator> logger)
    {
        _translation = translation;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GalleryTranslationResult>> TranslateAsync(
        IReadOnlyList<GalleryTranslationRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0) return Array.Empty<GalleryTranslationResult>();

        var hashes = requests.Select(r => TranslationSourceHasher.ComputeHash(r.NormalizedSource)).ToList();

        // Deduplicate identical sources so "Alpha" + "Alpha" costs one translated string, then map the
        // shared result back to every request that carried that source.
        var distinct = requests.Select(r => r.NormalizedSource).Distinct(StringComparer.Ordinal).ToList();

        IReadOnlyList<string>? translated = null;
        try
        {
            translated = await _translation.TranslateTextAsync(
                distinct, SourceLanguage, TargetLanguage, cancellationToken);

            if (translated.Count != distinct.Count)
            {
                // Never log the payload — only the counts.
                _logger.LogWarning(
                    "Gallery translation returned {ResultCount} results for {SourceCount} sources; marking FAILED.",
                    translated.Count, distinct.Count);
                translated = null;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the caller cancelled the whole request — do not swallow
        }
        catch (Exception ex)
        {
            // Provider not configured / HTTP error / timeout / quota / invalid payload → FAILED, never rethrow.
            _logger.LogWarning(ex,
                "Gallery translation provider call failed ({SourceCount} sources); Vietnamese data is saved, EN marked FAILED.",
                distinct.Count);
            translated = null;
        }

        var bySource = new Dictionary<string, string>(StringComparer.Ordinal);
        if (translated is not null)
        {
            for (var i = 0; i < distinct.Count; i++)
                bySource[distinct[i]] = translated[i];
        }

        var results = new List<GalleryTranslationResult>(requests.Count);
        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var text = bySource.TryGetValue(request.NormalizedSource, out var raw) ? raw?.Trim() : null;

            // A blank or over-cap translated string is invalid for THIS request — FAILED, never truncated.
            var valid = !string.IsNullOrWhiteSpace(text) && text!.Length <= request.MaxLength;
            results.Add(new GalleryTranslationResult
            {
                SourceText = request.NormalizedSource,
                SourceHash = hashes[i],
                TranslatedText = valid ? text : null,
                Success = valid,
            });
        }

        return results;
    }
}

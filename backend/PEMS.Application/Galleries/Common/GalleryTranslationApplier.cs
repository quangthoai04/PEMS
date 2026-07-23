using PEMS.Domain.Entities.Galleries;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Writes one <see cref="GalleryTranslationResult"/> onto an entity's persisted translation fields —
/// the ONLY place that knows the field mapping, so every handler behaves identically:
///   success → EN = translated, AUTO/READY, hash, translated_at = now;
///   failure → EN = NULL (a stale EN must never survive a source change), AUTO/FAILED, hash of the NEW
///             source, translated_at = NULL.
/// </summary>
public static class GalleryTranslationApplier
{
    public static void Apply(
        GalleryArea area, GalleryTranslationResult result, DateTime now,
        string source = GalleryTranslationSources.Auto)
    {
        area.AreaNameEn = result.TranslatedText;
        area.TranslationSource = source;
        area.TranslationStatus = result.Status;
        area.TranslationSourceHash = result.SourceHash;
        area.TranslatedAt = result.Success ? now : null;
    }

    public static void Apply(
        GalleryLocation location, GalleryTranslationResult result, DateTime now,
        string source = GalleryTranslationSources.Auto)
    {
        location.LocationNameEn = result.TranslatedText;
        location.TranslationSource = source;
        location.TranslationStatus = result.Status;
        location.TranslationSourceHash = result.SourceHash;
        location.TranslatedAt = result.Success ? now : null;
    }

    public static void Apply(
        GalleryItem item, GalleryTranslationResult result, DateTime now,
        string source = GalleryTranslationSources.Auto)
    {
        item.TitleEn = result.TranslatedText;
        item.TranslationSource = source;
        item.TranslationStatus = result.Status;
        item.TranslationSourceHash = result.SourceHash;
        item.TranslatedAt = result.Success ? now : null;
    }

    /// <summary>
    /// True when the stored metadata already matches this normalized source — i.e. READY, hash equal and
    /// a non-blank EN value — so the Translation API must NOT be called again (BR §6.4).
    /// </summary>
    public static bool IsUpToDate(
        string? translationStatus, string? storedHash, string? storedEn, string normalizedSource)
        => translationStatus == GalleryTranslationStatuses.Ready
           && !string.IsNullOrWhiteSpace(storedEn)
           && storedHash == TranslationSourceHasher.ComputeHash(normalizedSource);
}

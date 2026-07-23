using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Galleries.Common;

/// <summary>A client-supplied EN that may be persisted as-is (no provider call): the ready-to-apply
/// result + which <c>translation_source</c> to store (AUTO for a reused preview, MANUAL for a hand edit).</summary>
public sealed class ResolvedGalleryTranslation
{
    public ResolvedGalleryTranslation(GalleryTranslationResult result, string translationSource)
    {
        Result = result;
        TranslationSource = translationSource;
    }

    public GalleryTranslationResult Result { get; }

    /// <summary>AUTO (reused preview) or MANUAL (hand-typed/edited EN).</summary>
    public string TranslationSource { get; }
}

/// <summary>
/// Save-time decision for one bilingual name (BR §17 of the preview plan): can the EN carried by the
/// payload be persisted without calling Google again?
///   MANUAL + EN present            → yes, MANUAL/READY, hash recomputed from the current VI;
///   AUTO_PREVIEW + hash matches    → yes, AUTO/READY (the preview already paid the provider call);
///   AUTO_PREVIEW + hash mismatch   → GALLERY_TRANSLATION_PREVIEW_STALE (never silently re-translate);
///   anything else / EN blank       → null → the caller runs the legacy AUTO_ON_SAVE translation.
/// </summary>
public static class GalleryPreviewedTranslation
{
    public static ResolvedGalleryTranslation? TryResolve(
        string normalizedVi, string? clientEn, string? origin, string? providedHash, int maxLength)
    {
        var en = clientEn?.Trim();
        if (string.IsNullOrEmpty(en)) return null;

        var normalizedOrigin = (origin ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedOrigin is not (GalleryTranslationOrigins.Manual or GalleryTranslationOrigins.AutoPreview))
            return null;

        // Never truncate — an over-cap EN is a client input error (mirrors the coordinator's rule).
        if (en.Length > maxLength)
            throw new BusinessRuleException(
                $"Tên tiếng Anh tối đa {maxLength} ký tự.", GalleryErrorCodes.TranslationPreviewFailed);

        var currentHash = TranslationSourceHasher.ComputeHash(normalizedVi);

        if (normalizedOrigin == GalleryTranslationOrigins.AutoPreview && providedHash != currentHash)
            throw new BusinessRuleException(
                "Nội dung tiếng Việt đã thay đổi sau khi dịch. Vui lòng dịch lại trước khi lưu.",
                GalleryErrorCodes.TranslationPreviewStale);

        var result = new GalleryTranslationResult
        {
            SourceText = normalizedVi,
            SourceHash = currentHash,
            TranslatedText = en,
            Success = true,
        };
        var source = normalizedOrigin == GalleryTranslationOrigins.Manual
            ? GalleryTranslationSources.Manual
            : GalleryTranslationSources.Auto;
        return new ResolvedGalleryTranslation(result, source);
    }
}

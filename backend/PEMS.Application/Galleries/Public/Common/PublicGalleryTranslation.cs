using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Public.Common;

/// <summary>
/// The ONLY rule deciding what English string the anonymous public API exposes:
/// READY + non-blank EN → the EN value; anything else (PENDING / FAILED / OUTDATED / blank) → null so
/// the frontend falls back to Vietnamese. Public DTOs never expose the raw translation metadata
/// (source / status / hash / translated_at) — this helper collapses it server-side.
/// </summary>
public static class PublicGalleryTranslation
{
    public static string? EnOrNull(string? translationStatus, string? en)
        => translationStatus == GalleryTranslationStatuses.Ready && !string.IsNullOrWhiteSpace(en)
            ? en!.Trim()
            : null;
}

using MediatR;

namespace PEMS.Application.Galleries.Commands.PreviewGalleryItemTranslation;

/// <summary>
/// "Dịch sang EN" preview for the gallery item create/edit modals. Translates ONE Vietnamese field
/// (currently only the title) and returns the EN with the source hash the save can later reuse — this
/// endpoint NEVER writes anything (no DB, no files, no translation metadata).
/// <c>EntityId</c> is set by the EDIT modal so an unchanged, already-READY stored title is served from
/// the database (zero provider calls); the CREATE modal sends null.
/// </summary>
public sealed record PreviewGalleryItemTranslationCommand(
    string? EntityType,
    string? Field,
    long? EntityId,
    string? SourceText) : IRequest<GalleryItemTranslationPreviewDto>;

/// <summary>Accepted <c>entityType</c> / <c>field</c> values of the item preview endpoint.</summary>
public static class GalleryItemTranslationPreviewFields
{
    public const string EntityTypeGalleryItem = "GALLERY_ITEM";
    public const string FieldTitle = "TITLE";
}

/// <summary>Where the previewed EN came from: a live provider call or the stored, still-valid EN.</summary>
public static class GalleryTranslationPreviewSources
{
    public const string Google = "GOOGLE";
    public const string Database = "DATABASE";
}

/// <summary>The previewed field: normalized VI source, its SHA-256 hash and the EN translation.</summary>
public sealed class GalleryItemTranslationPreviewDto
{
    public string SourceText { get; init; } = string.Empty;
    public string SourceHash { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;
    /// <summary>GOOGLE (fresh provider call) or DATABASE (stored READY EN reused — zero quota).</summary>
    public string ServedFrom { get; init; } = GalleryTranslationPreviewSources.Google;
}

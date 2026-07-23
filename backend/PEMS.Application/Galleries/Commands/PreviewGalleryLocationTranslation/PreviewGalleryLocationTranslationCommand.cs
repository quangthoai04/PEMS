using MediatR;

namespace PEMS.Application.Galleries.Commands.PreviewGalleryLocationTranslation;

/// <summary>
/// "Dịch sang EN" preview for the area/location create + edit modals. Translates the supplied Vietnamese
/// name(s) in ONE provider request and returns the EN with the source hash the save can later reuse —
/// this endpoint NEVER writes anything (no DB, no files, no translation metadata).
/// <c>Mode</c>: EXISTING_AREA (location only), NEW_AREA (area + location), EDIT (per include flags).
/// </summary>
public sealed record PreviewGalleryLocationTranslationCommand(
    string Mode,
    string? AreaNameVi,
    string? LocationNameVi,
    bool? IncludeArea,
    bool? IncludeLocation) : IRequest<GalleryLocationTranslationPreviewDto>;

/// <summary>One previewed field: the normalized VI source, its SHA-256 hash and the EN translation.</summary>
public sealed class TranslationPreviewFieldDto
{
    public string SourceText { get; init; } = string.Empty;
    public string SourceHash { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;
}

/// <summary>Preview payload. A field not requested comes back null (e.g. Area on EXISTING_AREA).</summary>
public sealed class GalleryLocationTranslationPreviewDto
{
    public TranslationPreviewFieldDto? Area { get; init; }
    public TranslationPreviewFieldDto? Location { get; init; }
}

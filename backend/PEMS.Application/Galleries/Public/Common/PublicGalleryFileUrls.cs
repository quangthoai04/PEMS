namespace PEMS.Application.Galleries.Public.Common;

/// <summary>
/// Builds the <b>public</b> proxy URLs the VisitFPTU public page uses to display gallery media.
/// Unlike the authenticated <c>/api/files/{id}/content</c> endpoint, this route is anonymous but is
/// scoped server-side to media that belongs to a public-visible gallery item (BR-PGAL-13/14). The raw
/// Google Drive URL / file metadata is never handed to the client.
/// </summary>
internal static class PublicGalleryFileUrls
{
    public static string Content(ulong fileId) => $"/api/public/visit-fptu/media/{fileId}/content";

    public static string? ContentOrNull(ulong? fileId) => fileId.HasValue ? Content(fileId.Value) : null;

    /// <summary>
    /// Anonymous, item+language-scoped audio URL for the public speaker icon. The client never passes a
    /// raw fileId — the endpoint resolves the file from the gallery item + language. <paramref name="version"/>
    /// (the audio fileId) is appended as a cache-buster so a replaced recording is never served stale.
    /// </summary>
    public static string Audio(ulong galleryItemId, string languageCode, ulong version) =>
        $"/api/public/visit-fptu/gallery-items/{galleryItemId}/audio/{languageCode}?v={version}";
}

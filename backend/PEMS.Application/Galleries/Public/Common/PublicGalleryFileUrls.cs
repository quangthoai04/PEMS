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
}

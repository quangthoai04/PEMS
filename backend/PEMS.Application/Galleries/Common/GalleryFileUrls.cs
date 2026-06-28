namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Builds the backend proxy URLs the frontend uses to display gallery media. Every file is served
/// through <c>GET /api/files/{fileId}/content</c> (authenticated) — the raw Google Drive URL is never
/// handed to the client. See the Google Drive upload foundation doc.
/// </summary>
internal static class GalleryFileUrls
{
    public static string Content(ulong fileId) => $"/api/files/{fileId}/content";

    public static string? ContentOrNull(ulong? fileId) => fileId.HasValue ? Content(fileId.Value) : null;
}

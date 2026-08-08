namespace PEMS.Application.Partners.Common;

/// <summary>
/// Builds the backend proxy URL the frontend uses to display a partner's logo/cover. Every file is
/// served through <c>GET /api/files/{fileId}/content</c> (authenticated) — mirrors
/// <c>Galleries.Common.GalleryFileUrls</c>.
/// </summary>
internal static class PartnerFileUrls
{
    public static string Content(ulong fileId) => $"/api/files/{fileId}/content";

    public static string? ContentOrNull(ulong? fileId) => fileId.HasValue ? Content(fileId.Value) : null;
}

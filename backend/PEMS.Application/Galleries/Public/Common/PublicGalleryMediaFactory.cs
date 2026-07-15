using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Public.Common;

/// <summary>
/// Builds a <see cref="PublicGalleryMediaDto"/> from raw media + file metadata, resolving whether it is an
/// uploaded Drive file (served through the scoped public proxy) or an external YouTube reference (rendered
/// as an iframe, with its thumbnail served directly from YouTube — never the proxy). Callers project the
/// raw scalar columns in the DB query, then map in memory with this (source URLs can't be built in SQL).
/// </summary>
internal static class PublicGalleryMediaFactory
{
    public static PublicGalleryMediaDto Build(
        ulong mediaId,
        ulong fileId,
        string mediaType,
        ulong? thumbnailFileId,
        string? caption,
        string? altText,
        bool isPrimary,
        int displayOrder,
        string? filePurpose,
        string? externalFileId,
        string? webViewUrl,
        string? fileThumbnailUrl)
    {
        var source = GalleryMediaSourceResolver.Resolve(filePurpose, externalFileId, webViewUrl);
        var isYouTube = source.SourceType == GalleryMediaSourceTypes.YouTube;
        return new PublicGalleryMediaDto
        {
            MediaId = mediaId,
            FileId = fileId,
            MediaType = mediaType,
            SourceType = source.SourceType,
            Url = isYouTube ? null : PublicGalleryFileUrls.Content(fileId),
            ThumbnailUrl = isYouTube ? fileThumbnailUrl : PublicGalleryFileUrls.ContentOrNull(thumbnailFileId),
            YoutubeVideoId = source.YoutubeVideoId,
            EmbedUrl = source.EmbedUrl,
            WebViewUrl = source.WebViewUrl,
            Caption = caption,
            AltText = altText,
            IsPrimary = isPrimary,
            DisplayOrder = displayOrder,
        };
    }
}

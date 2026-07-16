using System;
using PEMS.Application.Common.Files;

namespace PEMS.Application.Galleries.Common;

/// <summary>The source-specific fields shared by the management and public media DTOs.</summary>
public readonly record struct GalleryMediaSourceInfo(
    string SourceType, string? YoutubeVideoId, string? EmbedUrl, string? WebViewUrl);

/// <summary>
/// Derives how a gallery media should be rendered from its <c>files</c> metadata. A row whose
/// <c>file_purpose = GALLERY_YOUTUBE_VIDEO</c> is an external YouTube reference (rendered as an iframe,
/// no content endpoint); anything else is an uploaded Drive file served through the proxy.
/// </summary>
public static class GalleryMediaSourceResolver
{
    public static bool IsYouTube(string? filePurpose) =>
        string.Equals(filePurpose, FilePurposeDbValues.GalleryYouTubeVideo, StringComparison.OrdinalIgnoreCase);

    public static GalleryMediaSourceInfo Resolve(string? filePurpose, string? externalFileId, string? webViewUrl)
    {
        if (IsYouTube(filePurpose) && !string.IsNullOrWhiteSpace(externalFileId))
        {
            var id = externalFileId!;
            return new GalleryMediaSourceInfo(
                GalleryMediaSourceTypes.YouTube,
                id,
                $"https://www.youtube-nocookie.com/embed/{id}",
                webViewUrl);
        }

        return new GalleryMediaSourceInfo(GalleryMediaSourceTypes.UploadedFile, null, null, null);
    }
}

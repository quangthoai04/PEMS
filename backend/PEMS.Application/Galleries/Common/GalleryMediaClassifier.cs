using System;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Classifies an uploaded file into a gallery media type. The backend never trusts the frontend's
/// "định dạng" dropdown — it derives <c>media_type</c> (and the upload <see cref="FilePurpose"/>) from the
/// actual file. The shared <see cref="IFileUploadService"/> still performs the authoritative size / MIME /
/// magic-byte validation; this only routes image vs video and rejects clearly-unsupported extensions early.
/// </summary>
internal static class GalleryMediaClassifier
{
    public const string Image = "IMAGE";
    public const string Video = "VIDEO";

    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] VideoExtensions = { ".mp4", ".webm" };

    /// <summary>
    /// Returns the media type ("IMAGE"/"VIDEO") and Drive upload purpose for a file, or throws
    /// <see cref="Common.Exceptions.BusinessRuleException"/> (422) if the extension/MIME is unsupported.
    /// </summary>
    public static (string MediaType, FilePurpose Purpose) Classify(string fileName, string? contentType)
    {
        var ext = GetExtension(fileName);
        var mime = (contentType ?? string.Empty).ToLowerInvariant();

        if (Array.IndexOf(ImageExtensions, ext) >= 0 || mime.StartsWith("image/"))
            return (Image, FilePurpose.GalleryImage);

        if (Array.IndexOf(VideoExtensions, ext) >= 0 || mime.StartsWith("video/"))
            return (Video, FilePurpose.GalleryVideo);

        throw new BusinessRuleException(
            $"Tệp \"{fileName}\" không phải ảnh hoặc video được hỗ trợ.",
            GalleryErrorCodes.InvalidMediaFile);
    }

    /// <summary>
    /// Computes <c>gallery_items.media_kind</c> from the media types present: all images → IMAGE,
    /// all videos → VIDEO, a mix → MIXED. Empty defaults to IMAGE.
    /// </summary>
    public static string ResolveMediaKind(System.Collections.Generic.IEnumerable<string> mediaTypes)
    {
        var hasImage = false;
        var hasVideo = false;
        foreach (var t in mediaTypes)
        {
            if (string.Equals(t, Video, StringComparison.OrdinalIgnoreCase)) hasVideo = true;
            else hasImage = true;
        }

        if (hasImage && hasVideo) return "MIXED";
        if (hasVideo) return "VIDEO";
        return "IMAGE";
    }

    private static string GetExtension(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot >= 0 ? fileName.Substring(dot).ToLowerInvariant() : string.Empty;
    }
}

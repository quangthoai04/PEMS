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
    /// Classifies an uploaded (machine) file. Only images are accepted from the machine — a supported
    /// image returns ("IMAGE", GalleryItemImage/GalleryDelegationImage per <paramref name="itemType"/>).
    /// A video file throws (422, <see cref="GalleryErrorCodes.VideoUploadNotAllowed"/>) — videos must be
    /// added via a YouTube link — and anything else throws
    /// <see cref="GalleryErrorCodes.InvalidMediaFile"/> (422).
    /// </summary>
    public static (string MediaType, FilePurpose Purpose) Classify(
        string fileName, string? contentType, string itemType)
    {
        var ext = GetExtension(fileName);
        var mime = (contentType ?? string.Empty).ToLowerInvariant();
        var isDelegation = string.Equals(
            itemType, GalleryItemTypes.VisitDelegation, StringComparison.OrdinalIgnoreCase);

        if (Array.IndexOf(ImageExtensions, ext) >= 0 || mime.StartsWith("image/"))
            return (Image, isDelegation ? FilePurpose.GalleryDelegationImage : FilePurpose.GalleryItemImage);

        // Uploading a video file from the machine is no longer allowed — videos are added via a YouTube
        // link instead. Detected here (before any Drive upload) so the user gets a clear, specific message
        // pointing them to the YouTube field rather than the generic "unsupported file" error.
        if (Array.IndexOf(VideoExtensions, ext) >= 0 || mime.StartsWith("video/"))
            throw new BusinessRuleException(
                $"Tệp \"{fileName}\" là video. Khi tải từ máy chỉ chấp nhận ảnh — vui lòng thêm video qua liên kết YouTube.",
                GalleryErrorCodes.VideoUploadNotAllowed);

        throw new BusinessRuleException(
            $"Tệp \"{fileName}\" không phải ảnh được hỗ trợ.",
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

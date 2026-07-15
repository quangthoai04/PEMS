using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Handles the single MP4 cover video attached to a <c>gallery_areas</c> row (master data — the Area
/// Showcase fullscreen background, NOT a gallery item). Each area has exactly one cover; that cover may
/// be an image (legacy areas) or a video (new areas). This helper validates the MP4 early so the Staff
/// Leader gets an area-cover-specific message, then hands the bytes to the shared
/// <see cref="IFileUploadService"/> (the authoritative size / MIME check) under
/// <see cref="FilePurpose.GalleryAreaCoverVideo"/>. Duration (≤ 120s) is enforced on the frontend — the
/// backend does not add FFmpeg/ffprobe.
/// </summary>
internal static class GalleryAreaCoverVideo
{
    private const long MaxSizeBytes = 100L * 1024 * 1024;

    /// <summary>Uploads one MP4 area-cover video and returns the new <c>files.file_id</c>.</summary>
    public static async Task<ulong> UploadAsync(
        IFileUploadService fileUpload,
        GalleryUploadFileCommandDto file,
        ulong actorId,
        CancellationToken ct)
    {
        EnsureMp4(file);

        await using var stream = new MemoryStream(file.Content, writable: false);
        var uploaded = await fileUpload.UploadBusinessFileAsync(
            stream, file.FileName, file.ContentType ?? string.Empty, file.FileSize,
            FilePurpose.GalleryAreaCoverVideo, (long)actorId, ct);
        return (ulong)uploaded.FileId;
    }

    private static void EnsureMp4(GalleryUploadFileCommandDto file)
    {
        if (file.Content is null || file.Content.Length == 0 || file.FileSize <= 0)
            throw new BusinessRuleException(
                "File video không hợp lệ hoặc đã bị hỏng.", GalleryErrorCodes.AreaCoverVideoInvalid);

        if (file.FileSize > MaxSizeBytes)
            throw new BusinessRuleException(
                "Video đại diện khu vực không được vượt quá 100 MB.", GalleryErrorCodes.AreaCoverVideoTooLarge);

        var ext = GetExtension(file.FileName);
        var mime = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        var isMp4 = ext == ".mp4" && (mime.Length == 0 || mime == "video/mp4");
        if (isMp4) return;

        throw new BusinessRuleException(
            "Video đại diện khu vực chỉ hỗ trợ định dạng MP4.", GalleryErrorCodes.AreaCoverVideoInvalid);
    }

    private static string GetExtension(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot >= 0 ? fileName.Substring(dot).ToLowerInvariant() : string.Empty;
    }
}

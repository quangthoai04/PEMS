using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Handles the single cover image attached to a <c>gallery_areas</c> / <c>gallery_locations</c> row
/// (master data — NOT a gallery item). Each cover is exactly one image; the shared
/// <see cref="IFileUploadService"/> performs the authoritative size / MIME / magic-byte validation, this
/// only rejects a clearly-non-image early so the user gets a cover-specific message.
/// </summary>
internal static class GalleryCoverImage
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>
    /// Uploads one cover image via the shared upload foundation and returns the new <c>files.file_id</c>.
    /// <paramref name="isArea"/> selects the purpose (area vs location cover) and the error message.
    /// </summary>
    public static async Task<ulong> UploadAsync(
        IFileUploadService fileUpload,
        GalleryUploadFileCommandDto file,
        bool isArea,
        ulong actorId,
        CancellationToken ct)
    {
        EnsureImage(file, isArea);

        var purpose = isArea ? FilePurpose.GalleryAreaCover : FilePurpose.GalleryLocationCover;
        await using var stream = new MemoryStream(file.Content, writable: false);
        var uploaded = await fileUpload.UploadBusinessFileAsync(
            stream, file.FileName, file.ContentType ?? string.Empty, file.FileSize, purpose, (long)actorId, ct);
        return (ulong)uploaded.FileId;
    }

    private static void EnsureImage(GalleryUploadFileCommandDto file, bool isArea)
    {
        var ext = GetExtension(file.FileName);
        var mime = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var isImage = Array.IndexOf(ImageExtensions, ext) >= 0 || mime.StartsWith("image/");
        if (isImage) return;

        throw isArea
            ? new BusinessRuleException(
                "Ảnh đại diện khu vực không đúng định dạng.", GalleryErrorCodes.AreaCoverInvalid)
            : new BusinessRuleException(
                "Ảnh đại diện vị trí không đúng định dạng.", GalleryErrorCodes.LocationCoverInvalid);
    }

    private static string GetExtension(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot >= 0 ? fileName.Substring(dot).ToLowerInvariant() : string.Empty;
    }
}

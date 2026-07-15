using System;
using PEMS.Application.Common.Files;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Infers whether an area cover file is an IMAGE (legacy areas) or a VIDEO (new areas) from its
/// <c>files</c> metadata — the DB reuses <c>gallery_areas.cover_file_id</c> for both and stores no
/// separate media-type column. A file is a VIDEO when its purpose is <c>GALLERY_AREA_COVER_VIDEO</c> or
/// its MIME type starts with <c>video/</c>; everything else is treated as an IMAGE (the default).
/// </summary>
public static class GalleryCoverMediaType
{
    public const string Image = "IMAGE";
    public const string Video = "VIDEO";

    /// <summary>Resolves the cover media type from the file's purpose and MIME type.</summary>
    public static string Resolve(string? filePurpose, string? mimeType)
    {
        var isVideo =
            string.Equals(filePurpose, FilePurposeDbValues.GalleryAreaCoverVideo, StringComparison.OrdinalIgnoreCase)
            || (mimeType is { Length: > 0 } && mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase));
        return isVideo ? Video : Image;
    }

    /// <summary>
    /// Resolves the media type for an area cover file id, given a lookup of file metadata. A missing
    /// entry (no cover, or a cover file row that could not be loaded) defaults to IMAGE so legacy/empty
    /// areas keep rendering an image background.
    /// </summary>
    public static string ResolveFor(
        ulong? coverFileId,
        System.Collections.Generic.IReadOnlyDictionary<ulong, (string? Purpose, string? Mime)> byFileId)
    {
        if (coverFileId is not { } id || !byFileId.TryGetValue(id, out var meta))
            return Image;
        return Resolve(meta.Purpose, meta.Mime);
    }
}

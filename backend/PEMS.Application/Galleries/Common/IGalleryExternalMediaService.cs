using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Registers an <b>external</b> (not-uploaded) media reference as a <c>files</c> metadata row so a gallery
/// item can point at it exactly like an uploaded file. Currently only YouTube: the video is never
/// downloaded to PEMS nor copied to Google Drive — only its canonical id/URLs are stored
/// (<c>storage_provider = OTHER</c>, <c>file_purpose = GALLERY_YOUTUBE_VIDEO</c>). Mirrors the shape of
/// <see cref="Common.Interfaces.IFileUploadService"/> so the Add/Edit handlers treat both the same way.
/// </summary>
public interface IGalleryExternalMediaService
{
    /// <summary>
    /// Validates + canonicalises the URL and writes the metadata <c>files</c> row, returning its id and
    /// resolved YouTube URLs. Throws <see cref="Common.Exceptions.BusinessRuleException"/> (422) on any
    /// invalid URL — call it BEFORE creating gallery rows so a bad URL never leaves orphans.
    /// </summary>
    Task<RegisteredExternalMediaResult> RegisterYouTubeAsync(
        string youtubeUrl, long uploadedBy, CancellationToken cancellationToken);
}

/// <summary>Result of registering an external media reference (a persisted <c>files</c> row).</summary>
public sealed record RegisteredExternalMediaResult(
    long FileId,
    string SourceType,
    string ExternalId,
    string CanonicalUrl,
    string EmbedUrl,
    string ThumbnailUrl,
    string MimeType);

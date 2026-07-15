using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Default <see cref="IGalleryExternalMediaService"/>. Persists a YouTube reference as a metadata-only
/// <c>files</c> row: no Google Drive upload, no binary, no YouTube download. The object key is built by the
/// shared <see cref="IFileObjectKeyBuilder"/> (unique) so it satisfies the <c>uq_files_object_key</c> index.
/// </summary>
public sealed class GalleryExternalMediaService : IGalleryExternalMediaService
{
    private const string StorageProviderOther = "OTHER";

    private readonly IApplicationDbContext _db;
    private readonly IFileObjectKeyBuilder _objectKeyBuilder;
    private readonly IDateTimeService _clock;

    public GalleryExternalMediaService(
        IApplicationDbContext db, IFileObjectKeyBuilder objectKeyBuilder, IDateTimeService clock)
    {
        _db = db;
        _objectKeyBuilder = objectKeyBuilder;
        _clock = clock;
    }

    public async Task<RegisteredExternalMediaResult> RegisterYouTubeAsync(
        string youtubeUrl, long uploadedBy, CancellationToken cancellationToken)
    {
        // Validate + canonicalise first (throws 422 on bad input — no DB write happens then).
        var video = YouTubeUrlParser.Parse(youtubeUrl);

        var objectKey = _objectKeyBuilder.Build(
            FilePurpose.GalleryYouTubeVideo, uploadedBy, video.OriginalFileName);

        var file = new UploadedFile
        {
            StorageProvider = StorageProviderOther,
            ObjectKey = objectKey,
            OriginalFilename = video.OriginalFileName,
            MimeType = GalleryMediaSourceTypes.YouTubeMimeType,
            FileSize = null, // no binary — external reference only
            ExternalFileId = video.VideoId,
            WebViewUrl = video.WatchUrl,
            ThumbnailUrl = video.ThumbnailUrl,
            FilePurpose = FilePurposeDbValues.GalleryYouTubeVideo,
            UploadedBy = (ulong)uploadedBy,
            UploadedAt = _clock.VietnamNow,
        };

        _db.Files.Add(file);
        await _db.SaveChangesAsync(cancellationToken); // assigns file.FileId

        return new RegisteredExternalMediaResult(
            FileId: (long)file.FileId,
            SourceType: GalleryMediaSourceTypes.YouTube,
            ExternalId: video.VideoId,
            CanonicalUrl: video.WatchUrl,
            EmbedUrl: video.EmbedUrl,
            ThumbnailUrl: video.ThumbnailUrl,
            MimeType: GalleryMediaSourceTypes.YouTubeMimeType);
    }
}

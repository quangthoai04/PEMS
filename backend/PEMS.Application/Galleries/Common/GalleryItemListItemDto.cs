using System;

namespace PEMS.Application.Galleries.Common;

/// <summary>One row of the Staff Leader gallery list (UC-GAL-01 / UC-GAL-02).</summary>
public sealed class GalleryItemListItemDto
{
    public ulong GalleryItemId { get; init; }
    public ulong AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public ulong LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ItemType { get; init; } = string.Empty;
    public string ItemTypeLabel { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string? CreatedByName { get; init; }
    public GalleryPrimaryMediaDto? PrimaryMedia { get; init; }

    /// <summary>
    /// EverAI narration status for the item's CURRENT description — one of
    /// <see cref="PEMS.Application.Galleries.Tts.TtsManagementStatuses"/>
    /// (READY / PROCESSING / FAILED / STALE / NOT_CREATED / DISABLED / INVALID_DESCRIPTION). Drives the
    /// list "AUDIO" column and the audio-status filter.
    /// </summary>
    public string AudioStatus { get; init; } = PEMS.Application.Galleries.Tts.TtsManagementStatuses.NotCreated;
}

/// <summary>Lightweight primary-media projection shown as the thumbnail in the list.</summary>
public sealed class GalleryPrimaryMediaDto
{
    public ulong MediaId { get; init; }
    public ulong FileId { get; init; }
    public string MediaType { get; init; } = string.Empty;
    /// <summary>UPLOADED_FILE or YOUTUBE.</summary>
    public string SourceType { get; init; } = GalleryMediaSourceTypes.UploadedFile;
    /// <summary>Proxy content URL for an uploaded file; null for a YouTube reference.</summary>
    public string? FileUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? YoutubeVideoId { get; init; }
    public string? EmbedUrl { get; init; }
    public string? WebViewUrl { get; init; }
}

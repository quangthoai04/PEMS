using System.Text.Json.Serialization;
using MediatR;

namespace PEMS.Application.Galleries.Tts.Queries.GetGalleryItemTtsStatus;

/// <summary>
/// Staff Leader dashboard status of a gallery item's narration
/// (<c>GET /api/gallery-management/items/{id}/tts-audio</c>). Read-only, campus-scoped; drives the
/// audio-status badge and enables/disables the "Tạo lại audio" button.
/// </summary>
public sealed record GetGalleryItemTtsStatusQuery(long GalleryItemId)
    : IRequest<GalleryItemTtsStatusResponse>;

/// <summary>
/// Response of the management status query. <see cref="Status"/> is one of
/// <see cref="TtsManagementStatuses"/>; <see cref="CanRegenerate"/> is false when the current
/// description already has matching READY audio (up to date) or a job is already running.
/// </summary>
public sealed class GalleryItemTtsStatusResponse
{
    public string Status { get; init; } = TtsManagementStatuses.NotCreated;

    public bool CanRegenerate { get; init; }

    /// <summary>Authenticated <c>/api/files/{id}/content</c> URL when READY (the dashboard has a session).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AudioUrl { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VoiceCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AudioType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; init; }
}

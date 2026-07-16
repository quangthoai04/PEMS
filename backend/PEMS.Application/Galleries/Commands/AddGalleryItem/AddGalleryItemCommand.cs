using System.Collections.Generic;
using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

/// <summary>
/// UC-GAL-04 Add Gallery Item / Upload Media (Staff Leader). The controller buffers the multipart files
/// into <see cref="Files"/>. Campus, area, media_kind, file metadata and audit fields are all derived
/// server-side — the client only sends title / description / locationId / status / files / youtubeUrls.
/// At least one media source (an uploaded file OR a YouTube URL) is required.
/// </summary>
/// <param name="YoutubeUrls">YouTube video URLs to attach as external VIDEO media (0..N).</param>
/// <param name="PrimaryMediaKey">
/// Which media becomes primary: <c>upload:{index}</c> (into <see cref="Files"/>) or
/// <c>youtube:{index}</c> (into <see cref="YoutubeUrls"/>). Null → the first media is primary.
/// </param>
public sealed record AddGalleryItemCommand(
    string Title,
    string Description,
    long LocationId,
    string? ItemType,
    string? Status,
    IReadOnlyList<GalleryUploadFileCommandDto> Files,
    IReadOnlyList<string> YoutubeUrls,
    string? PrimaryMediaKey) : IRequest<GalleryItemDetailDto>;

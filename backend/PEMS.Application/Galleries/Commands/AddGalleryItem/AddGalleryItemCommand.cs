using System.Collections.Generic;
using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

/// <summary>
/// UC-GAL-04 Add Gallery Item / Upload Media (Staff Leader). The controller buffers the multipart files
/// into <see cref="Files"/>. Campus, area, media_kind, file metadata and audit fields are all derived
/// server-side — the client only sends title / description / locationId / status / files.
/// </summary>
public sealed record AddGalleryItemCommand(
    string Title,
    string Description,
    long LocationId,
    string? ItemType,
    string? Status,
    IReadOnlyList<GalleryUploadFileCommandDto> Files) : IRequest<GalleryItemDetailDto>;

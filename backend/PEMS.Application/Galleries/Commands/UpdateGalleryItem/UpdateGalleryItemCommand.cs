using System.Collections.Generic;
using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryItem;

/// <summary>
/// UC-GAL-07 Edit Gallery Item (Staff Leader). Updates metadata and reconciles media: media ids in
/// <see cref="KeepMediaIds"/> are kept, the rest are soft-deleted, and <see cref="NewFiles"/> are
/// uploaded and appended. Status (PUBLISHED/HIDDEN) is never changed here. Campus / location / media
/// rules are enforced server-side.
/// </summary>
public sealed record UpdateGalleryItemCommand(
    long GalleryItemId,
    string Title,
    string Description,
    long LocationId,
    string? ItemType,
    IReadOnlyList<long> KeepMediaIds,
    IReadOnlyList<GalleryUploadFileCommandDto> NewFiles,
    long? PrimaryMediaId) : IRequest<GalleryItemDetailDto>;

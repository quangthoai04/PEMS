using System.Collections.Generic;
using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

/// <summary>
/// UC-GAL-04 Add Gallery Item / Upload Media (Staff Leader). The controller buffers the multipart files
/// into <see cref="Files"/> and the two mandatory audio recordings into <see cref="AudioVi"/> /
/// <see cref="AudioEn"/>. Campus, area, media_kind, file metadata and audit fields are all derived
/// server-side. A gallery item is only valid with ALL FOUR bilingual fields (descriptionVi + audioVi +
/// descriptionEn + audioEn) plus at least one gallery media (an uploaded image OR a YouTube URL).
/// </summary>
/// <param name="YoutubeUrls">YouTube video URLs to attach as external VIDEO media (0..N).</param>
/// <param name="PrimaryMediaKey">
/// Which media becomes primary: <c>upload:{index}</c> (into <see cref="Files"/>) or
/// <c>youtube:{index}</c> (into <see cref="YoutubeUrls"/>). Null → the first media is primary.
/// </param>
/// <param name="TitleEn">Previewed/manual English title. Empty → backend auto-translates on save.</param>
/// <param name="TitleTranslationOrigin">Where <paramref name="TitleEn"/> came from
/// (<see cref="GalleryTranslationOrigins"/>) — decides whether the save may reuse it.</param>
/// <param name="TitleTranslationSourceHash">Preview source hash — AUTO_PREVIEW is only reusable while
/// it still matches the current Vietnamese title.</param>
public sealed record AddGalleryItemCommand(
    string Title,
    string? TitleEn,
    string? TitleTranslationOrigin,
    string? TitleTranslationSourceHash,
    string DescriptionVi,
    GalleryUploadFileCommandDto? AudioVi,
    string DescriptionEn,
    GalleryUploadFileCommandDto? AudioEn,
    long LocationId,
    string? ItemType,
    string? Status,
    IReadOnlyList<GalleryUploadFileCommandDto> Files,
    IReadOnlyList<string> YoutubeUrls,
    string? PrimaryMediaKey) : IRequest<GalleryItemDetailDto>;

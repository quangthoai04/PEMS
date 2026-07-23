using System.Collections.Generic;
using MediatR;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryItem;

/// <summary>
/// UC-GAL-07 Edit Gallery Item (Staff Leader). Updates metadata + bilingual content and reconciles media:
/// media ids in <see cref="KeepMediaIds"/> are kept, the rest are soft-deleted, <see cref="NewFiles"/> are
/// uploaded + appended, and <see cref="YoutubeUrls"/> are registered + appended as external VIDEO media.
/// Both descriptions are always required. Each audio is kept unless a replacement is supplied
/// (<see cref="NewAudioVi"/> / <see cref="NewAudioEn"/>) — audio can never be removed. Status
/// (PUBLISHED/HIDDEN) is never changed here. Campus / location / media rules are enforced server-side.
/// </summary>
/// <param name="YoutubeUrls">New YouTube video URLs to add as external VIDEO media (0..N).</param>
/// <param name="PrimaryMediaKey">
/// Which media becomes primary: <c>existing:{mediaId}</c> (a kept media), <c>upload:{index}</c> (into
/// <see cref="NewFiles"/>) or <c>youtube:{index}</c> (into <see cref="YoutubeUrls"/>). When null,
/// <see cref="PrimaryMediaId"/> (a kept media id) is used for backward compatibility; failing both, the
/// first active media is primary.
/// </param>
/// <param name="TitleEn">Previewed/manual English title. Empty → kept/auto-translated server-side.</param>
/// <param name="TitleTranslationOrigin">Where <paramref name="TitleEn"/> came from
/// (<see cref="GalleryTranslationOrigins"/>) — decides whether the save may reuse it.</param>
/// <param name="TitleTranslationSourceHash">Preview source hash — AUTO_PREVIEW is only reusable while
/// it still matches the current Vietnamese title.</param>
public sealed record UpdateGalleryItemCommand(
    long GalleryItemId,
    string Title,
    string? TitleEn,
    string? TitleTranslationOrigin,
    string? TitleTranslationSourceHash,
    string DescriptionVi,
    string DescriptionEn,
    GalleryUploadFileCommandDto? NewAudioVi,
    GalleryUploadFileCommandDto? NewAudioEn,
    long LocationId,
    string? ItemType,
    IReadOnlyList<long> KeepMediaIds,
    IReadOnlyList<GalleryUploadFileCommandDto> NewFiles,
    long? PrimaryMediaId,
    IReadOnlyList<string> YoutubeUrls,
    string? PrimaryMediaKey) : IRequest<GalleryItemDetailDto>;

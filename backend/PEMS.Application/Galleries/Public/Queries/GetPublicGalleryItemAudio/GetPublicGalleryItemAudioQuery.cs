using MediatR;
using PEMS.Application.Galleries.Public.Queries.GetPublicGalleryMediaStream;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicGalleryItemAudio;

/// <summary>
/// Anonymous audio proxy for the public speaker icon. The client never passes a raw fileId — the audio
/// file is resolved server-side from the gallery item id + language code (vi/en). The item must be
/// public-visible (PUBLISHED, not deleted, location/area/campus ACTIVE), have a content row, and the
/// resolved file must be a GALLERY_AUDIO file. Anything else is a controlled 404. Streams the bytes
/// (optionally a byte range) — reuses <see cref="PublicGalleryMediaStreamResult"/>.
/// </summary>
public sealed record GetPublicGalleryItemAudioQuery(
    long GalleryItemId, string LanguageCode, long? RangeFrom, long? RangeTo)
    : IRequest<PublicGalleryMediaStreamResult>;

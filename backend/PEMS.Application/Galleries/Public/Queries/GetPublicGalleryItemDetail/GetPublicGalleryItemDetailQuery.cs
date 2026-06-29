using MediatR;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicGalleryItemDetail;

/// <summary>
/// UC §5 (ITEM_DETAIL) — one public-visible gallery item with its full ordered ACTIVE media list
/// (BR-PGAL-GRID-07). Throws 404 when the item is not public-visible (hidden/deleted, or its
/// location/area/campus is inactive — BR-PGAL-GRID-10/11). Anonymous.
/// </summary>
public sealed record GetPublicGalleryItemDetailQuery(long GalleryItemId)
    : IRequest<PublicGalleryItemDetailDto>;

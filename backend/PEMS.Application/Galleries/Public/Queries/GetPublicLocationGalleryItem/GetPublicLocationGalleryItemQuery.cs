using MediatR;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicLocationGalleryItem;

/// <summary>
/// UC §4 (LOCATION_GRID) — the album grid of a location: every public-visible gallery item represented
/// by its primary media (BR-PGAL-GRID-01/02). Throws 404 when the location has no public-visible item
/// (BR-PGAL-GRID-11). Anonymous.
/// </summary>
public sealed record GetPublicLocationGalleryItemsQuery(long LocationId)
    : IRequest<PublicLocationGalleryGridDto>;

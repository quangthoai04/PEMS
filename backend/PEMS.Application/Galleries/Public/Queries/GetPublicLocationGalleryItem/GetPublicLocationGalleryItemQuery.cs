using MediatR;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicLocationGalleryItem;

/// <summary>
/// UC §7.3 — the (single) public-visible gallery item of a location, with its ordered media list.
/// Throws 404 when the location/item is not public-visible (AF in §10.3, BR-PGAL-22). Anonymous.
/// </summary>
public sealed record GetPublicLocationGalleryItemQuery(long LocationId)
    : IRequest<PublicGalleryItemDetailDto>;

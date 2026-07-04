using MediatR;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicLocationShowcase;

/// <summary>
/// Location Showcase — the public-visible gallery items of one location, split by item_type into the
/// right-hand MEDIA column and the "Đoàn khách đã tới thăm" (VISIT_DELEGATION) row, each represented by
/// its primary media. Returns null (→ 404) only when the location itself is not public-visible
/// (location/area/campus ACTIVE); an ACTIVE location with no items returns empty lists. Anonymous.
/// </summary>
public sealed record GetPublicLocationShowcaseQuery(long LocationId)
    : IRequest<PublicLocationShowcaseDto?>;
